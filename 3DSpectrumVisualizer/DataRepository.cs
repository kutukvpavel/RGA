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
using Avalonia.Data.Converters;

namespace _3DSpectrumVisualizer
{
    public class DataRepository
    {
        public static ParallelOptions ParallelOptions { get; set; } = new ParallelOptions()
        {
            MaxDegreeOfParallelism = 4
        };
        public static SKColor FallbackColor { get; set; } = SKColor.Parse("#84FFFFFF");
        public static SKColor[] LightGradient { get; set; } = new SKColor[]
        {
            SKColor.Parse("#00FAF4F4"),
            SKColor.Parse("#8F7B7B7B")
        };

        public DataRepository(string folder, int pollPeriod = 3000)
        {
            StartTime = DateTime.Now;
            EndTime = StartTime;
            Folder = folder;
            _PollTimer = new System.Timers.Timer(pollPeriod) { AutoReset = true, Enabled = false };
            _PollTimer.Elapsed += _PollTimer_Elapsed;
            PaintFill.Shader = Shader;
            PaintStroke.Shader = Shader;
        }

        public event EventHandler DataAdded;

        #region Properties

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
        public ICollection<SKColor> ColorScheme { get; set; } = new SKColor[0];
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public float Duration { get => (float)(EndTime - StartTime).TotalSeconds; }
        public float Min { get; private set; } = 1;
        public float Max { get; private set; } = 0;
        public float Left { get; private set; } = 1;
        public float Right { get; private set; } = 0;
        public float MidX { get => (Right - Left) / 2; }
        public float MidY { get => (Max - Min) / 2; }
        public bool LogarithmicIntensity { get; set; } = false;
        public SKPath MassAxis { get; private set; } = new SKPath();
        public SKPath TimeAxis { get; private set; } = new SKPath();

        public bool Enabled
        {
            get { return _PollTimer.Enabled; }
            set { if (value) _PollTimer.Start(); else _PollTimer.Stop(); }
        }

        #endregion

        #region Public Methods

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

        public void UpdateData()
        {
            bool raiseDataAdded = false;
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
                            raiseDataAdded = true;
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
            if (raiseDataAdded) DataAdded?.Invoke(this, new EventArgs());
        }

        public void RecalculateShader()
        {
            if (ColorScheme.Count > 1)
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, LogarithmicIntensity ? MathF.Log10(Min) : Min),
                    new SKPoint(0, LogarithmicIntensity ? MathF.Log10(Max) : Max),
                    ColorScheme.ToArray(),
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

        #region Private

        private DateTime _LastFileCreationTime = DateTime.MinValue;
        private readonly System.Timers.Timer _PollTimer;
        private void _PollTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            UpdateData();
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
            var ct = DateTime.Parse(item.Name.Replace('_', ' ')
                .Replace("Scan", "", StringComparison.InvariantCultureIgnoreCase)
                .Trim(),
                CultureInfo.InvariantCulture
                );
            if (Results.Count == 0) StartTime = ct;
            else EndTime = ct;
            var sr = new ScanResult(File.ReadLines(item.FullName), item.Name, ct);
            var b = sr.Path2D.TightBounds;
            bool updateShader = false;
            bool updateMassAxis = false;
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
                updateMassAxis = true;
            }
            if (b.Right > Right)
            {
                Right = b.Right;
                updateShader = true;
                updateMassAxis = true;
            }
            if (updateShader) RecalculateShader();
            if (updateMassAxis) RecalculateMassAxis();
            Results.Add(sr);
            RecalculateTimeAxis();
        }

        private void RecalculateMassAxis()
        {
            MassAxis.Reset();
            MassAxis.LineTo(Right, 0);
        }

        private void RecalculateTimeAxis()
        {
            TimeAxis.Reset();
            TimeAxis.LineTo(0, Duration);
        }

        #endregion
    }

    public class ScanResult
    {
        public static char Delimeter { get; set; } = ',';
        public static CultureInfo CurrentCulture { get; set; } = CultureInfo.InvariantCulture;

        private static readonly string PlacholderName = "N/A";

        public ScanResult()
        {
            Name = PlacholderName;
            CreationTime = DateTime.Now;
        }
        public ScanResult(IEnumerable<string> rawLines, string name, DateTime creationTime)
        {
            Name = name;
            CreationTime = creationTime;
            Path2D = new SKPath();
            LogPath2D = new SKPath();
            Parse(rawLines);
        }

        public SKPath Path2D { get; private set; }
        public SKPath LogPath2D { get; private set; }
        public string Name { get; }
        public DateTime CreationTime { get; }

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
                            if (Path2D.Points.Length > 0)
                            {
                                if (temp[1] <= 0)
                                {
                                    Path2D.LineTo(temp[0], Path2D.LastPoint.Y);
                                    LogPath2D.LineTo(temp[0], LogPath2D.LastPoint.Y);
                                }
                                else
                                {
                                    Path2D.LineTo(temp[0], temp[1]);
                                    LogPath2D.LineTo(temp[0], MathF.Log10(temp[1]));
                                }
                            }
                            else
                            {
                                if (temp[1] <= 0) continue;
                                Path2D.MoveTo(temp[0], temp[1]);
                                LogPath2D.MoveTo(temp[0], MathF.Log10(temp[1]));
                            }
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
