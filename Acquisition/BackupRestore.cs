using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MoreLinq;

namespace Acquisition.BackupRestore
{
    public class BackupData
    {
        public static readonly string DefaultRestoreLocation = @$"..{Path.DirectorySeparatorChar}restored";

        public BackupData(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Backup directory '{path}' can not be found.");
            FolderPath = path;
            Sensor.LogException += Sensor_LogException;
        }

        public event EventHandler<ExceptionEventArgs> LogException;

        public string FolderPath { get; }
        public SortedList<DateTime, Spectrum> Spectra { get; private set; }
        public SortedList<int, Sensor> Sensors { get; private set; }
        public CsvConfiguration CsvConfig { get; set; } = new CsvConfiguration(CultureInfo.InvariantCulture);

        public void Load(string searchPattern)
        {
            var files = Directory.GetFiles(FolderPath, searchPattern);
            Spectra = new SortedList<DateTime, Spectrum>(files.Length);
            foreach (var item in files)
            {
                try
                {
                    Spectra.Add(ParseFileName(item),
                        new Spectrum(item, CsvConfig, Spectra.Count > 0 ? Spectra.Last().Value.DataPoints.Count : 65));
                }
                catch (Exception ex)
                {
                    LogException?.Invoke(this, new ExceptionEventArgs(ex, "Failed to parse a backup file."));
                }
            }
            try
            {
                files = Directory.GetFiles(FolderPath, "*.gpib_sensor");
                Sensors = new SortedList<int, Sensor>(files.Length);
                foreach (var item in files)
                {
                    try
                    {
                        Sensors.Add(int.Parse(Path.GetFileNameWithoutExtension(item)), new GpibSensor(item));
                    }
                    catch (Exception ex)
                    {
                        LogException?.Invoke(this, new ExceptionEventArgs(ex, "Failed to parse a SENSOR backup file."));
                    }
                }
            }
            catch (Exception ex)
            {
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Failed to get SENSOR backup files."));
            }
        }

        public void SaveWith(string targetRelPath, Configuration cfg)
        {
            string targetFolder = FolderPath.EndsWith(Path.DirectorySeparatorChar) ?
                FolderPath : FolderPath + Path.DirectorySeparatorChar;
            targetFolder = Path.GetFullPath(targetFolder + targetRelPath);
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
            MovingAverageContainer[] containers = new MovingAverageContainer[cfg.GapEnabled ? 2 : 1];
            int capacity = Spectra.Values.Max(x => x.DataPoints.Count) + 1;
            for (int i = 0; i < containers.Length; i++)
            {
                containers[i] = new MovingAverageContainer(cfg.MovingAverageWindowWidth, capacity);
            }
            int containerAlignmentRecal = 0;
            for (int i = 0; i < Spectra.Count; i++)
            {
                try
                {
                    var s = Spectra.Values[i];
                    var x = s.DataPoints.Keys;
                    var c = containers[(i + containerAlignmentRecal) % containers.Length];
                    if ((c.Length != x.Count) && (c.Count > 0))
                    {
                        LogException?.Invoke(this, new ExceptionEventArgs(new Exception(
                            "Warning: gap agorithm malfunction detected, missing half of a spectrum.")));
                        int k = 0;
                        for (; k < containers.Length; k++)
                        {
                            if (containers[k].Length == x.Count)
                            {
                                containerAlignmentRecal = k - ((i + containerAlignmentRecal) % containers.Length);
                                c = containers[(i + containerAlignmentRecal) % containers.Length];
                                break;
                            }
                        }
                        if (k == containers.Length)
                        {
                            LogException?.Invoke(this, new ExceptionEventArgs(new Exception(
                                "Warning: can't find suitable averaging container for this spectrum length."),
                                x.Count.ToString()));
                            continue;
                        }
                    }
                    c.Enqueue(s.DataPoints.Values);
                    double div = cfg.CdemGain * (s.TotalPressure == 0 ? cfg.CdemEnabledAdditionalDivisionFactor : s.TotalPressure);
                    var res = c.CurrentAverage.Select(x => x / div);
                    using TextWriter tw = new StreamWriter(
                        Path.Combine(targetFolder, string.Format(cfg.FileNameFormat, Spectra.Keys[i]))
                        );
                    using CsvWriter cw = new CsvWriter(tw, CsvConfig);
                    cw.NextRecord();
                    cw.NextRecord();
                    int j = 0;
                    foreach (var item in res)
                    {
                        cw.WriteField(x[j++].ToString(cfg.AMUFormat, CultureInfo.InvariantCulture));
                        cw.WriteField(item.ToString(cfg.IntensityFormat, CultureInfo.InvariantCulture));
                        cw.NextRecord();
                    }
                }
                catch (Exception ex)
                {
                    LogException?.Invoke(this, new ExceptionEventArgs(ex, "Failed to save a restored file."));
                }
            }
            foreach (var item in Sensors)
            {
                try
                {
                    var sensorFilePath = Path.Combine(targetFolder, $"info{Path.DirectorySeparatorChar}Sensor{item.Key}.txt");
                    string dir = Path.GetDirectoryName(sensorFilePath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    using var sensorFile = File.CreateText(sensorFilePath);
                    foreach (var line in item.Value.Data)
                    {
                        sensorFile.WriteLine(
                            $"{line.Key.ToString(CultureInfo.InvariantCulture)} | {line.Value}");
                    }
                    sensorFile.Close();
                }
                catch (Exception ex)
                {
                    LogException?.Invoke(this, new ExceptionEventArgs(ex, "Failed to save a restored SENSOR file."));
                }
            }
        }

        public static DateTime ParseFileName(string path)
        {
            string ts = Path.GetFileNameWithoutExtension(path).Replace('_', ' ')
                .Replace("Scan", "", StringComparison.InvariantCultureIgnoreCase).Trim();
            int timeIndex = ts.IndexOf(' ');
            ts = new string(ts.Select((x, i) => i > timeIndex ? (x == '-' ? ':' : x) : x).ToArray()); //Replace dashes with colons for time string
            return DateTime.Parse(ts, CultureInfo.InvariantCulture);
        }

        private void Sensor_LogException(object sender, ExceptionEventArgs e)
        {
            LogException?.Invoke(this, e);
        }
    }

    public abstract class Sensor
    {
        public static event EventHandler<ExceptionEventArgs> LogException;

        public SortedList<DateTime, double> Data { get; protected set; }

        protected static void RaiseLogException(object sender, Exception ex, string message = null)
        {
            LogException?.Invoke(sender, new ExceptionEventArgs(ex, message));
        }
    }
    public class GpibSensor : Sensor
    {
        public GpibSensor(string filePath)
        {
            Data = new SortedList<DateTime, double>((int)(new FileInfo(filePath).Length / 89 + 1));
            Parse(filePath);
        }

        private void Parse(string path)
        {
            var lines = File.ReadLines(path);
            lines = lines.Skip(1);
            foreach (var item in lines)
            {
                try
                {
                    string[] halves = item.Split(';');
                    DateTime dateTime = DateTime.Parse(halves[0], CultureInfo.InvariantCulture);
                    string[] values = halves[1].Split(',');
                    Data.Add(dateTime, double.Parse(values[1], NumberStyles.Float | NumberStyles.AllowLeadingSign));
                }
                catch (Exception ex)
                {
                    RaiseLogException(this, ex, "Failed to parse sensor data line.");
                }
            }
        }
    }

    public class Spectrum
    {
        public Spectrum(string filePath, CsvConfiguration cfg, int capacity)
        {
            DataPoints = new SortedList<double, double>(capacity + 1);
            CsvConfig = cfg;
            Parse(filePath);
        }

        public SortedList<double, double> DataPoints { get; }
        public double TotalPressure { get; private set; }

        private CsvConfiguration CsvConfig;

        private void Parse(string path)
        {
            var lines = File.ReadLines(path);
            int consequtiveEmptyLines = 0;
            foreach (var item in lines)
            {
                if (item.Length > 0)
                {
                    if (consequtiveEmptyLines >= 2)
                    {
                        //Reached the data segment
                        try
                        {
                            var split = item.Split(CsvConfig.Delimiter);
                            DataPoints.Add(NumberParse(split[0]), NumberParse(split[1]));
                        }
                        catch (FormatException)
                        {

                        }
                    }
                    else
                    {
                        consequtiveEmptyLines = 0;
                    }
                }
                else
                {
                    consequtiveEmptyLines++;
                }
            }
            var last = DataPoints.Last();
            TotalPressure = last.Value;
            DataPoints.Remove(last.Key);
        }

        private double NumberParse(string src)
        {
            return double.Parse(src,
                                NumberStyles.AllowLeadingWhite |
                                NumberStyles.AllowTrailingWhite |
                                NumberStyles.AllowExponent |
                                NumberStyles.AllowDecimalPoint |
                                NumberStyles.AllowLeadingSign,
                                CsvConfig.CultureInfo);
        }
    }
}
