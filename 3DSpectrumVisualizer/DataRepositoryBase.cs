using CsvHelper;
using CsvHelper.Configuration;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace _3DSpectrumVisualizer
{
    public class SKPointXEqualityComparer : IEqualityComparer<SKPoint>
    {
        public bool Equals(SKPoint x, SKPoint y)
        {
            return x.X == y.X;
        }

        public int GetHashCode(SKPoint obj)
        {
            return obj.X.GetHashCode();
        }
    }

    public abstract class DataRepositoryBase
    {
        protected DataRepositoryBase(string location)
        {
            Location = location;
            StartTime = DateTime.Now;
            EndTime = StartTime;
            PaintStroke.Shader = Shader;
            PaintWideStroke.Shader = Shader;
        }

        public event EventHandler DataAdded;

        #region Abstract

        public abstract void Initialize();
        public abstract void Purge();
        public abstract void OpenDescriptionFile();
        public abstract void OpenRepoLocation();
        protected abstract void LoadDataInternal();
        public abstract bool CanPurge { get; protected set; }

        #endregion

        #region Static

        public static string TemperatureExportFormat { get; set; } = "F1";
        public static string SensorExportFormat { get; set; } = "E3";
        public static float ColorPositionSliderPrecision { get; set; }
        public static CsvConfiguration ExportCsvConfig { get; set; } = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            Delimiter = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ";" : ","
        };
        public static string InfoSplitter { get; set; }
        public static string InfoSubfolder { get; set; }
        public static string BackupDataSubfolder { get; set; } = "backup";
        public static string TemperatureFileName { get; set; }
        public static string UVFileName { get; set; }
        public static string GasFileName { get; set; }
        public static string SensorFileName { get; set; }
        public static string InfoFileName { get; set; }
        public static int VIModeVoltageIndex { get; set; }
        public static int VIModeCurrentIndex { get; set; }
        public static float VIModeCurrentMultiplier { get; set; }
        public static bool UseHorizontalGradient { get; set; } = false;
        public static int AMURoundingDigits { get; set; }
        public static ParallelOptions ParallelOptions { get; set; } = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        public static SKColor FallbackColor { get; set; }
        public static SKColor[] LightGradient { get; set; }
        public static SKPaint RegionPaintTemplate { get; set; } = new SKPaint()
        {
            Color = SKColor.Parse("#fff"),
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            StrokeWidth = 0
        };
        protected static SKPointXEqualityComparer XEqualityComparer = new SKPointXEqualityComparer();
        protected static Func<GradientColor, float> ColorPosSelector =
            new Func<GradientColor, float>(x => x.Position);

        public static void ExportVI(IList<DataRepositoryBase> repos, float startFraction, float endFraction, string path)
        {
            bool lockTaken = false;
            foreach (var item in repos)
            {
                lockTaken = Monitor.TryEnter(item.UpdateSynchronizingObject, 20000);
                if (!lockTaken) break;
            }
            if (!lockTaken)
            {
                foreach (var item in repos)
                {
                    try
                    {
                        Monitor.Exit(item.UpdateSynchronizingObject);
                    }
                    catch (SynchronizationLockException)
                    { }
                }
                throw new TimeoutException("Can't acquire update lock to export the section.");
            }
            try
            {
                using TextWriter tw = new StreamWriter(path, false);
                using CsvWriter cw = new CsvWriter(tw, ExportCsvConfig);
                int len = repos.Max(x => x.VIModeTimestamps.Count);
                int start = (int)MathF.Round(len * startFraction);
                int end = (int)MathF.Round(len * endFraction);
                int[] tempIndex = new int[repos.Count];
                foreach (var item in repos)
                {
                    cw.WriteField(item.Location.Split(Path.DirectorySeparatorChar).LastOrDefault());
                    cw.WriteField("T");
                    cw.WriteField("UV");
                    cw.WriteField("V");
                    cw.WriteField("I");
                }
                cw.NextRecord();
                for (int i = start; i < end; i++)
                {
                    try
                    {
                        for (int j = 0; j < repos.Count; j++)
                        {
                            var item = repos[j];
                            float t = item.VIModeTimestamps[i];
                            cw.WriteField(t.ToString(Program.Config.ExportXFormat));
                            //Temperature
                            cw.WriteField(FindProfilePoint(item.TemperatureProfile, t, ref tempIndex[j], TemperatureExportFormat));
                            //UV
                            cw.WriteField(item.HitTestUVRegion(t) ?
                                Program.Config.ExportUVTrueString : Program.Config.ExportUVFalseString);
                            if (item.VIModeTimestamps.Count > i)
                            {
                                cw.WriteField(item.VIModeProfile[i].X.ToString(SensorExportFormat));
                                cw.WriteField(item.VIModeProfile[i].Y.ToString(SensorExportFormat));
                            }
                            else
                            {
                                cw.WriteField(string.Empty);
                                cw.WriteField(string.Empty);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.LogException(repos, ex);
                    }
                    cw.NextRecord();
                }
            }
            finally
            {
                foreach (var item in repos)
                {
                    Monitor.Exit(item.UpdateSynchronizingObject);
                }
            }
        }

        public static void ExportSections(IList<DataRepositoryBase> repos, float amu, string path)
        {
            bool lockTaken = false;
            foreach (var item in repos)
            {
                lockTaken = Monitor.TryEnter(item.UpdateSynchronizingObject, 20000);
                if (!lockTaken) break;
            }
            if (!lockTaken)
            {
                foreach (var item in repos)
                {
                    try
                    {
                        Monitor.Exit(item.UpdateSynchronizingObject);
                    }
                    catch (SynchronizationLockException)
                    { }
                }
                throw new TimeoutException("Can't acquire update lock to export the section.");
            }
            try
            {
                using TextWriter tw = new StreamWriter(path, false);
                using CsvWriter cw = new CsvWriter(tw, ExportCsvConfig);
                int len = repos.Max(x => x.Sections[amu].LinearPath.PointCount);
                int[] tempIndex = new int[repos.Count];
                List<int[]> sensorIndex = new List<int[]>(repos.Count);
                foreach (var item in repos)
                {
                    cw.WriteField(item.Location.Split(Path.DirectorySeparatorChar).LastOrDefault());
                    cw.WriteField($"AMU = {amu:F1}");
                    cw.WriteField("Temperature");
                    cw.WriteField("UV");
                    cw.WriteField("Gas");
                    for (int i = 0; i < item.SensorProfiles.Count; i++)
                    {
                        cw.WriteField($"Sensor{i}");
                    }
                    sensorIndex.Add(new int[item.SensorProfiles.Count]);
                }
                cw.NextRecord();
                for (int i = 0; i < len; i++)
                {
                    try
                    {
                        for (int j = 0; j < repos.Count; j++)
                        {
                            var item = repos[j];
                            if (item.Sections[amu].LinearPath.PointCount > i)
                            {
                                var p = item.Sections[amu].LinearPath[i];
                                //Time and value
                                cw.WriteField(p.X.ToString(Program.Config.ExportXFormat));
                                cw.WriteField(p.Y.ToString(Program.Config.ExportYFormat));
                                //Temperature
                                string t = FindProfilePoint(item.TemperatureProfile, p.X, ref tempIndex[j], TemperatureExportFormat);
                                cw.WriteField(t);
                                //UV
                                cw.WriteField(item.HitTestUVRegion(p.X) ?
                                    Program.Config.ExportUVTrueString : Program.Config.ExportUVFalseString);
                                //Gas
                                t = item.HitTestGasRegion(p.X);
                                cw.WriteField(t ?? "");
                                //Sensors
                                for (int k = 0; k < item.SensorProfiles.Count; k++)
                                {
                                    t = FindProfilePoint(item.SensorProfiles[k], p.X, ref sensorIndex[j][k], SensorExportFormat);
                                    cw.WriteField(t);
                                }
                            }
                            else
                            {
                                int columns = 5 + item.SensorProfiles.Count;
                                for (int k = 0; k < columns; k++)
                                {
                                    cw.WriteField(string.Empty);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.LogException(repos, ex);
                    }
                    cw.NextRecord();
                }
            }
            finally
            {
                foreach (var item in repos)
                {
                    Monitor.Exit(item.UpdateSynchronizingObject);
                }
            }
        }

        private static string FindProfilePoint(SKPath profile, float timeStamp, ref int index, string format)
        {
            string t = "";
            try
            {
                if (profile[index].X <= timeStamp)
                {
                    while (profile[index].X < timeStamp) index++;
                    t = profile[index].Y.ToString(format);
                }
            }
            catch (IndexOutOfRangeException)
            { }
            return t;
        }

        #endregion

        #region Properties

        public virtual bool Enabled { get; set; }
        public object UpdateSynchronizingObject { get; set; } = new object();
        public string Location { get; }
        public string Filter { get; set; }
        public List<ScanResult> Results { get; } = new List<ScanResult>();
        public List<SKPath> SensorProfiles { get; } = new List<SKPath>();
        public List<SKPath> LogSensorProfiles { get; } = new List<SKPath>();
        public SKPath TemperatureProfile { get; } = new SKPath();
        public SKPath VIModeProfile { get; } = new SKPath();
        public List<float> VIModeTimestamps { get; } = new List<float>();
        public List<UVRegion> UVProfile { get; } = new List<UVRegion>();
        public List<GasRegion> GasProfile { get; } = new List<GasRegion>();
        public SKPaint PaintStroke { get; set; } = new SKPaint()
        {
            Color = FallbackColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0,
            IsAntialias = false
        };
        public SKPaint VIPaint { get; set; } = new SKPaint()
        {
            Color = FallbackColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0,
            IsAntialias = false
        };
        public SKPaint PaintWideStroke { get; set; } = new SKPaint()
        {
            Color = FallbackColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.1f,
            IsAntialias = true
        };
        protected SKPaint _SectionPaintBuffer = new SKPaint()
        {
            BlendMode = SKBlendMode.SrcOver,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeWidth = 0
        };
        public SKPaint SectionPaint
        {
            get
            {
                _SectionPaintBuffer.Color = PaintStroke.Color;
                return _SectionPaintBuffer;
            }
        }
        public SKShader Shader { get; protected set; }
        public SKPaint UVRegionPaint { get; set; } = new SKPaint()
        {
            BlendMode = RegionPaintTemplate.BlendMode,
            IsAntialias = RegionPaintTemplate.IsAntialias,
            Style = RegionPaintTemplate.Style,
            StrokeWidth = RegionPaintTemplate.StrokeWidth
        };
        public Dictionary<string, SKColor> GasRegionColor { get; set; } = new Dictionary<string, SKColor>();
        public ICollection<GradientColor> ColorScheme { get; set; } = new GradientColor[0];
        public SKPaint[] SensorColors { get; set; } = new SKPaint[0];
        public SKPaint TemperaturePaint { get; set; } = new SKPaint()
        {
            Color = FallbackColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0,
            IsAntialias = true
        };
        public DateTime StartTime { get; protected set; }
        public DateTime EndTime { get; protected set; }
        public float Duration { get => (float)(EndTime - StartTime).TotalSeconds; }
        public float AverageScanTime { get; protected set; } = 0;
        public float Min { get; protected set; } = float.MaxValue;
        public float PositiveMin { get; protected set; } = float.MaxValue;
        public float Max { get; protected set; } = float.MinValue;
        public float Left { get; protected set; } = float.MaxValue;
        public float Right { get; protected set; } = 0;
        public float MidX { get => Left + (Right - Left) / 2; }
        public float MidY { get => Min + (Max - Min) / 2; }
        public bool LogarithmicIntensity { get; set; } = false;
        public bool SensorLogScale { get; set; } = false;
        public SKPath MassAxis { get; protected set; } = new SKPath();
        public SKPath TimeAxis { get; protected set; } = new SKPath();
        public Dictionary<float, SpectrumSection> Sections { get; protected set; } = new Dictionary<float, SpectrumSection>();
        public int VILastAddedCurrentIndex { get; protected set; } = 0;
        public int VILastAddedVoltageIndex { get; protected set; } = 0;
        public float VIModeDuration => VIModeTimestamps.LastOrDefault() - VIModeTimestamps.FirstOrDefault();

        #endregion

        #region Public Methods

        public void LoadData()
        {
            LoadDataInternal();
            LoadDataFinished();
            RaiseDataAdded(this);
        }
        public void RecalculateShader()
        {
            float min = LogarithmicIntensity ? MathF.Log10(PositiveMin) : Min;
            float max = LogarithmicIntensity ? MathF.Log10(Max) : Max;
            if (ColorScheme.Count > 1 || float.IsNaN(min) || float.IsNaN(max))
            {
                SKColor[] colors = ColorScheme.Select(x => x.Color).ToArray();
                float[] positions = ColorScheme.Select(LogarithmicIntensity ?
                    /* This is linear gradient color position mapping to log scale.
                     * Obviously it won't change the gradient transition law, but
                     * it helps to reduce the necessity to adjust color positions
                     * when (inter)changing scales.
                     * Mapping function has a steep asymptotic behavior at negativeValuesColorPositionEdge,
                     * therefore all the values to the left from negativeValuesColorPositionEdge have to be
                     * interpolated (linearly). Two parts are stitched together using the fact that the slider has
                     * finite step resolution.
                     */
                    (x => {
                        float negativeValuesColorPositionEdge = (PositiveMin - Min) / (Max - Min);
                        float ratio = Max / PositiveMin;
                        float diff = x.Position - negativeValuesColorPositionEdge;
                        if (diff <= 0)
                            return MathF.Log10(ColorPositionSliderPrecision * (ratio - 1) + 1)
                            / MathF.Log10(ratio) * (1 + diff / negativeValuesColorPositionEdge);
                        return MathF.Log10((x.Position - negativeValuesColorPositionEdge) * (ratio - 1) + 1)
                            / MathF.Log10(ratio);
                    })
                    : ColorPosSelector).ToArray();
                Shader = SKShader.CreateLinearGradient(
                    UseHorizontalGradient ? new SKPoint(Left, 0) : new SKPoint(0, min),
                    UseHorizontalGradient ? new SKPoint(Right, 0) : new SKPoint(0, max),
                    colors,
                    positions,
                    SKShaderTileMode.Clamp
                    );
                _SectionPaintBuffer.Shader = UseHorizontalGradient ?
                    SKShader.CreateColor(FallbackColor) :
                    SKShader.CreateLinearGradient(new SKPoint(0, min), new SKPoint(0, max),
                        colors.Select(x => x.WithAlpha(0xFF)).ToArray(),
                        positions,
                        SKShaderTileMode.Clamp);
            }
            else
            {
                Shader = SKShader.CreateColor(FallbackColor);
                _SectionPaintBuffer.Shader = Shader;
            }
            if (LightGradient.Length > 1)
            {
                var lightShader = SKShader.CreateLinearGradient(
                    UseHorizontalGradient ? new SKPoint(0, min) : new SKPoint(Left, 0),
                    UseHorizontalGradient ? new SKPoint(0, max) : new SKPoint(Right, 0),
                    LightGradient,
                    SKShaderTileMode.Clamp
                    );
                Shader = SKShader.CreateCompose(Shader, lightShader, SKBlendMode.Darken);
            }
            PaintStroke.Shader = Shader;
            PaintWideStroke.Shader = Shader;
        }

        public bool HitTestUVRegion(double offset)
        {
            if (UVProfile.Count == 0) return false;
            return UVProfile.Any(x => (x.StartTimeOffset <= offset) && (x.EndTimeOffset >= offset));
        }
        public bool HitTestUVRegion(DateTime point)
        {
            double offset = (point - StartTime).TotalSeconds;
            return HitTestUVRegion(offset);
        }

        public string HitTestGasRegion(double offset)
        {
            if (GasProfile.Count == 0) return null;
            try
            {
                return GasProfile.First(x => (x.StartTimeOffset <= offset) && (x.EndTimeOffset >= offset)).Name;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

        }
        public string HitTestGasRegion(DateTime point)
        {
            double offset = (point - StartTime).TotalSeconds;
            return HitTestGasRegion(offset);
        }

        #endregion

        #region Protected Methods

        protected void LoadDataFinished()
        {
            var p = GasProfile.LastOrDefault();
            if (p != null)
            {
                p.Complete((float)(EndTime - StartTime).TotalSeconds);
            }
            if (Program.Config.EnableVI && (SensorProfiles.Count > VIModeCurrentIndex && SensorProfiles.Count > VIModeVoltageIndex))
            {
                VIPaint.Color = SensorColors[Program.Config.VIModeCurrentSensorIndex].Color;
                if (VIModeProfile.PointCount == 0)
                {
                    var intersection = SKRect.Intersect(SensorProfiles[VIModeVoltageIndex].Bounds, SensorProfiles[VIModeCurrentIndex].Bounds);
                    if (!intersection.IsEmpty)
                    {
                        VILastAddedVoltageIndex = SensorProfiles[VIModeVoltageIndex].Points.TakeWhile(x => x.X < intersection.Left).Count();
                        VILastAddedCurrentIndex = SensorProfiles[VIModeCurrentIndex].Points.TakeWhile(x => x.X < intersection.Left).Count();
                        var voltages = SensorProfiles[VIModeVoltageIndex].Points.Skip(VILastAddedVoltageIndex).TakeWhile(x => x.X <= intersection.Right);
                        var currents = SensorProfiles[VIModeCurrentIndex].Points.Skip(VILastAddedCurrentIndex).TakeWhile(x => x.X <= intersection.Right);
                        if (voltages.First().X > currents.First().X)
                        {
                            currents = currents.Skip(1);
                            VILastAddedCurrentIndex++;
                        }
                        VIModeProfile.MoveTo(new SKPoint(
                            voltages.First().Y,
                            currents.First().Y * VIModeCurrentMultiplier
                        ));
                        VIModeTimestamps.Add(currents.First().X);
                        voltages = voltages.Skip(1);
                        var voltageIterator = voltages.GetEnumerator();
                        voltageIterator.MoveNext();
                        currents = currents.Skip(1);
                        foreach (var item in currents)
                        {
                            float v = float.NaN;
                            do
                            {
                                if (voltageIterator.Current.X > item.X) break;
                                v = voltageIterator.Current.Y;
                                VILastAddedVoltageIndex++;
                            } while (voltageIterator.MoveNext());
                            VILastAddedCurrentIndex++;
                            if (!float.IsFinite(v)) continue;
                            VIModeProfile.LineTo(new SKPoint(v, item.Y));
                            VIModeTimestamps.Add(item.X);
                        }
                    }
                }
                else
                {
                    for (int i = VILastAddedCurrentIndex + 1; i < SensorProfiles[VIModeCurrentIndex].PointCount; i++)
                    {
                        SKPoint item = SensorProfiles[VIModeCurrentIndex][i];
                        float v = float.NaN;
                        for (int j = VILastAddedVoltageIndex + 1; j < SensorProfiles[VIModeVoltageIndex].PointCount; j++)
                        {
                            var voltagePoint = SensorProfiles[VIModeVoltageIndex][j];
                            if (voltagePoint.X > item.X) break;
                            v = voltagePoint.Y;
                            VILastAddedVoltageIndex++;
                        }
                        VILastAddedCurrentIndex++;
                        if (!float.IsFinite(v)) continue;
                        VIModeProfile.LineTo(new SKPoint(v, item.Y));
                        VIModeTimestamps.Add(item.X);
                    }
                }
            }
        }
        protected IEnumerable<string> ReadLines(StreamReader r)
        {
            string line;
            while ((line = r.ReadLine()) != null)
            {
                yield return line;
            }
        }

        protected void RecalculateMassAxis()
        {
            MassAxis.Reset();
            MassAxis.LineTo(Right, 0);
        }

        protected void RecalculateTimeAxis()
        {
            TimeAxis.Reset();
            TimeAxis.LineTo(0, Duration);
        }

        protected void RaiseDataAdded(object sender)
        {
            DataAdded?.Invoke(sender, new EventArgs());
        }

        protected void AddFile(IEnumerable<string> lines, string name)
        {
            string ts = Path.GetFileNameWithoutExtension(name).Replace('_', ' ')
                .Replace("Scan", "", StringComparison.InvariantCultureIgnoreCase).Trim();
            char[] arr = ts.ToCharArray();
            for (int i = ts.IndexOf(' '); i < arr.Length; i++) //Replace dashes with colons for time string
            {
                if (arr[i] == '-') arr[i] = ':';
            }
            var ct = DateTime.Parse(arr, CultureInfo.InvariantCulture);
            if (Results.Count == 0) StartTime = ct;
            else
            {
                EndTime = ct;
                AverageScanTime = Duration / (Results.Count + 1);
            }
            var sr = new ScanResult(lines, name, ct);
            var b = sr.Path2D.Bounds;
            bool updateShader = false;
            bool updateMassAxis = false;
            if (b.Bottom > Max)
            {
                Max = b.Bottom;
                updateShader = true;
            }
            if (sr.PositiveTopBound < PositiveMin)
            {
                PositiveMin = sr.PositiveTopBound;
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
            foreach (var point in sr.Path2D.Points
                .Select(x => new SKPoint(MathF.Round(x.X, AMURoundingDigits), x.Y))
                .Distinct(XEqualityComparer))
            {
                try
                {
                    var ss = Sections[point.X];
                    ss.AddPoint((float)(sr.CreationTime - StartTime).TotalSeconds, point.Y);
                }
                catch (KeyNotFoundException)
                {
                    Sections.Add(point.X, new SpectrumSection(point.Y));
                }
            }
        }

        private Dictionary<string, SKPaint> _GasPaintCache = new Dictionary<string, SKPaint>();
        protected void AddGasInfoLine(string l)
        {
            l = ParseInfoLine(l, out float t);
            var reg = GasProfile.LastOrDefault();
            if (reg != null)
            {
                reg.Complete(t);
                if (reg.Name == l) return;
            }
            if (GasRegionColor.ContainsKey(l))
            {
                if (!_GasPaintCache.ContainsKey(l)) _GasPaintCache.Add(l, new SKPaint()
                {
                    BlendMode = RegionPaintTemplate.BlendMode,
                    Color = GasRegionColor[l],
                    IsAntialias = RegionPaintTemplate.IsAntialias,
                    Style = RegionPaintTemplate.Style,
                    StrokeWidth = RegionPaintTemplate.StrokeWidth
                });
                reg = new GasRegion(this, t, l, _GasPaintCache[l]);
            }
            else
            {
                reg = new GasRegion(this, t, l);
            }
            GasProfile.Add(reg);
        }
        protected bool _LastUVState = false;
        protected void AddUVInfoLine(string l)
        {
            l = ParseInfoLine(l, out float t);
            bool val = bool.Parse(l);
            var lastRegion = UVProfile.LastOrDefault();
            if (_LastUVState ^ val)
            {
                if (val)
                {
                    UVProfile.Add(new UVRegion(this, t));
                }
                else
                {
                    if (lastRegion != null) lastRegion.Complete(t);
                }
            }
            /*else
            {
                if (val && (lastRegion != null)) lastRegion.Complete(t);
            }*/
            _LastUVState = val;
        }
        protected void AddTempInfoLine(string l)
        {
            l = ParseInfoLine(l, out float t);
            float val = float.Parse(l, CultureInfo.InvariantCulture);
            if (TemperatureProfile.PointCount > 0)
            {
                TemperatureProfile.LineTo(t, val);
            }
            else
            {
                TemperatureProfile.MoveTo(t, val);
            }
        }

        protected void AddSensorInfoLine(string l, int index)
        {
            l = ParseInfoLine(l, out float t);
            float val = float.Parse(l, CultureInfo.InvariantCulture);
            float logVal = val > 0 ? MathF.Log10(val) : float.NaN;
            while (SensorProfiles.Count <= index)
            {
                SensorProfiles.Add(new SKPath());
                LogSensorProfiles.Add(new SKPath());
            }
            var p = SensorProfiles[index];
            var lp = LogSensorProfiles[index];
            if (p.PointCount > 0)
            {
                p.LineTo(t, val);
            }
            else
            {
                p.MoveTo(t, val);
            }
            if (float.IsFinite(logVal))
            {
                if (lp.PointCount > 0)
                {
                    lp.LineTo(t, logVal);
                }
                else
                {
                    lp.MoveTo(t, logVal);
                }
            }
        }
        protected string ParseInfoLine(string l, out float time)
        {
            var split = l.Split(InfoSplitter);
            time = (float)(DateTime.Parse(split[0], CultureInfo.InvariantCulture) - StartTime).TotalSeconds;
            return split[1];
        }
        protected int FindPathStartIndex(SKPath p, float t)
        {
            for (int i = 0; i < p.PointCount; i++)
            {
                if (p[i].X >= t) return i;
            }
            return -1;
        }
        #endregion
    }
}
