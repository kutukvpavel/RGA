using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;
using MoreLinq;
using System.Threading.Tasks;

namespace _3DSpectrumVisualizer
{
    public class DataRepository
    {
        public static ParallelOptions ParallelOptions { get; set; } = new ParallelOptions()
        {
            MaxDegreeOfParallelism = 4
        };
        public static SKColor FallbackColor { get; set; } = SKColor.Parse("#84F7EFEF");
        public static SKColor[] LightGradient { get; set; } = new SKColor[]
        {
            SKColor.Parse("#00FFFFFF"),
            /*SKColor.Parse("#647B7B7B"),*/
            SKColor.Parse("#8F7B7B7B")
        };

        public DataRepository(string folder, int pollPeriod = 3000)
        {
            Folder = folder;
            _PollTimer = new System.Timers.Timer(pollPeriod) { AutoReset = true, Enabled = false };
            _PollTimer.Elapsed += _PollTimer_Elapsed;
            PaintFill.Shader = Shader;
            PaintStroke.Shader = Shader;
        }

        public object UpdateSynchronizingObject { get; set; } = new object();
        //public bool Throttle { get; set; } = false;
        public string Folder { get; }
        public string Filter { get; set; } = "*.csv";
        public List<ScanResult> Results { get; } = new List<ScanResult>();
        public SKPaint PaintFill { get; set; } = new SKPaint() 
        { 
            Color = FallbackColor, 
            Style = SKPaintStyle.Fill, 
            IsAntialias = false
        };
        public SKPaint PaintStroke { get; set; } = new SKPaint() 
        { 
            Color = FallbackColor, 
            Style = SKPaintStyle.Stroke, 
            StrokeWidth = 0,
            IsAntialias = false
        };
        public SKShader Shader { get; private set; }
        public SKColor[] ColorScheme { get; set; } = new SKColor[0];
        public float Min { get; private set; } = 0;
        public float Max { get; private set; } = 0;
        public float Left { get; private set; } = 0;
        public float Right { get; private set; } = 0;
        public float MidX { get => (Right - Left) / 2; }
        public float MidY { get => (Max - Min) / 2; }

        public bool Enabled
        {
            get { return _PollTimer.Enabled; }
            set { if (value) _PollTimer.Start(); else _PollTimer.Stop(); }
        }

        public Tuple<double[], double[], double[]> Get3DPoints()
        {
            double[] x = new double[0], y = new double[0], z = new double[0];
            lock (UpdateSynchronizingObject)
            {
                Parallel.Invoke(
                    () =>
                    {
                        x = Results.Select(x => x.Path2D.Points.Select(x => (double)x.X))
                            .Flatten().Cast<double>().ToArray();
                    },
                    () =>
                    {
                        z = Results.Select(x => x.Path2D.Points.Select(x => (double)x.Y))
                            .Flatten().Cast<double>().ToArray();
                    },
                    () =>
                    {
                        y = Results.Select((x, i) => Enumerable.Repeat((double)i, x.Path2D.PointCount))
                            .Flatten().Cast<double>().ToArray();
                    }
                );
            }
            return new Tuple<double[], double[], double[]>(x, y, z);
        }

        #region Private

        private DateTime _LastFileCreationTime = DateTime.MinValue;
        private readonly System.Timers.Timer _PollTimer;
        private void _PollTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            bool lockTaken = Monitor.TryEnter(UpdateSynchronizingObject, 10);
            if (!lockTaken) return;
            try
            {
                var newFiles = Directory.EnumerateFiles(Folder, Filter, SearchOption.TopDirectoryOnly)
                    .AsParallel().Select(x => new FileInfo(x)).Where(x => x.CreationTimeUtc > _LastFileCreationTime)
                    .OrderBy(x => x.Name);
                foreach (var item in newFiles)
                {
                    try
                    {
                        if (!FileIsInUse(item))
                        {
                            AddFile(item);
                            if (item.CreationTimeUtc > _LastFileCreationTime)
                                _LastFileCreationTime = item.CreationTimeUtc;
                        }
                    }
                    catch (Exception)
                    {
                        Results.Add(new ScanResult());
                    }
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                Monitor.Exit(UpdateSynchronizingObject);
            }
        }
        private bool FileIsInUse(FileInfo file)
        {
            try
            {
                using (FileStream s = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    s.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }
        private void AddFile(FileInfo item)
        {
            var sr = new ScanResult(File.ReadLines(item.FullName), item.Name);
            var b = sr.Path2D.Bounds;
            bool updateShader = false;
            if (b.Bottom > Max)
            {
                Max = b.Bottom;
                updateShader = true;
            }
            if (b.Top < Min)
            {
                Min = b.Top;
                updateShader = true;
            }
            if (b.Left < Left)
            {
                Left = b.Left;
                updateShader = true;
            }
            if (b.Right > Right)
            {
                Right = b.Right;
                updateShader = true;
            }
            if (updateShader)
            {
                if (ColorScheme.Length > 1)
                {
                    Shader = SKShader.CreateLinearGradient(
                        new SKPoint(0, Min),
                        new SKPoint(0, Max),
                        ColorScheme,
                        SKShaderTileMode.Clamp
                        );
                }
                else
                {
                    Shader = SKShader.CreateColor(FallbackColor);
                }
                if (LightGradient.Length > 1)
                {
                    var lightShader = SKShader.CreateLinearGradient(
                        new SKPoint(Left, 0),
                        new SKPoint(Right, 0),
                        LightGradient,
                        SKShaderTileMode.Clamp
                        );
                    Shader = SKShader.CreateCompose(Shader, lightShader, SKBlendMode.Darken);
                }
                PaintFill.Shader = Shader;
                PaintStroke.Shader = Shader;
            }
            Results.Add(sr);
        }

        #endregion
    }

    public class ScanResult
    {
        public static char Delimeter { get; set; } = ',';
        public static CultureInfo CurrentCulture { get; set; } = CultureInfo.InvariantCulture;
        public static bool ClipNegativeValues { get; set; } = true;

        private static readonly string PlacholderName = "N/A";

        public ScanResult()
        {
            Name = PlacholderName;
        }
        public ScanResult(IEnumerable<string> rawLines, string name)
        {
            Name = name;
            Path2D = new SKPath();
            Parse(rawLines);
        }

        public SKPath Path2D { get; private set; }
        public string Name { get; }

        private void Parse(IEnumerable<string> rawLines)
        {
            int consequtiveEmptyLines = 0;
            foreach (var item in rawLines)
            {
                var i = item.Trim('\r', '\n', ' ', Delimeter);
                if (consequtiveEmptyLines == 2)
                {
                    if (i.Length > 0)
                    {
                        //We've reached the data segment
                        try
                        {
                            var temp = i.Split(Delimeter).Select(x =>
                                float.Parse(x, 
                                NumberStyles.AllowLeadingWhite | 
                                NumberStyles.AllowTrailingWhite | 
                                NumberStyles.AllowExponent | 
                                NumberStyles.AllowDecimalPoint |
                                NumberStyles.AllowLeadingSign, 
                                CurrentCulture))
                                .ToArray();
                            Path2D.LineTo(temp[0], (temp[1] < 0 && ClipNegativeValues) ? 0 : temp[1]);
                        }
                        catch (FormatException)
                        {
                            
                        }
                    }
                    else
                    {
                        //EOF
                        break;
                    }
                }
                else
                {
                    //Going through instrument info segment
                    consequtiveEmptyLines = (i.Length > 0) ? 0 : (consequtiveEmptyLines + 1);
                }
            }
        }
    }
}
