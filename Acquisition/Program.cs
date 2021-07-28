using CsvHelper;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LLibrary;
using System.Collections.Generic;

namespace Acquisition
{
    public class Program
    {
        private static bool CancellationRequested = false;
        private static string StartAMU = "1";
        private static string EndAMU = "65";
        private static NamedPipeService Pipe = NamedPipeService.Instance;
        private static L Logger = new L();
        private static Configuration Config;
        
        public static Dictionary<double, double> Background { get; private set; } = new Dictionary<double, double>();
        public static string GapStartAMU { get; private set; } = null;
        public static string GapEndAMU { get; private set; } = null;
        public static Head Device { get; private set; }

        static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            //Load Settings
            try
            {
                Config = Serializer.Deserialize(Serializer.SettingsFileName, Config);
            }
            catch (Exception ex)
            {
                Log("Can't deserilize settings file:", ex);
                Config = new Configuration();
            }
            //Init
            InitDevice(args[0]);
            InitPipe(Config.PipeName);
            VerifyDirectoryExists(Configuration.WorkingDirectory);
            VerifyDirectoryExists(Configuration.WorkingDirectory, Config.BackupSubfolderName);
            VerifyDirectoryExists(Configuration.WorkingDirectory, Config.InfoSubfolderName);
            InitBackgroundRemoval();
            Console.WriteLine("RGA Acquisition Helper v1.0 started!");
            //CLI
            try
            {
                CommandSet.SetStartAMU.Parameter = args[1];
                CommandSet.SetEndAMU.Parameter = args[2];
                CommandSet.TurnHVON.Parameter = args[3];
                GapStartAMU = args[4];
                GapEndAMU = args[5];
            }
            catch (IndexOutOfRangeException)
            {

            }
            bool gap = false;
            if (GapStartAMU != null && GapEndAMU != null)
            {
                gap = true;
                StartAMU = args[1];
                EndAMU = args[2];
            }
            //Acquisition cycle
            try
            {
#if DEBUG_STEP
                Console.WriteLine("Press enter to start...");
                ConsoleKey lastKey = ConsoleKey.Enter;
                while (lastKey == ConsoleKey.Enter || lastKey == ConsoleKey.Spacebar)
                {
                    StateMachine();
                    Console.WriteLine("Confirm execution of the next sequence...");
                    lastKey = Console.ReadKey().Key;
                    if (lastKey == ConsoleKey.Spacebar) Device.StartScan();
                }
                Console.WriteLine("Aborted.");
                Device.AbortScan();
#else
                while (true)
                {
                    StateMachine();
                    Thread.Sleep(500);
                    if (Device.State == HeadState.DetectorON)
                    {
                        Console.WriteLine("Starting new scan...");
                        if (CancellationRequested) break;
                        if (gap)
                        {
                            ToggleAroundGap();
                        }
                        Device.StartScan();
                    }
                }
#endif
            }
            finally
            {
                Device.Dispose();
            }
        }

        #region Misc

        public static void Log(string info, object exception = null)
        {
            Console.WriteLine(info);
            Logger.Info(info);
            if (exception != null) Logger.Error(exception);
        }
        private static void VerifyDirectoryExists(params string[] path)
        {
            var d = Path.Combine(path);
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
        }

        private static void ToggleAroundGap()
        {
            if (CommandSet.SetEndAMU.Parameter == EndAMU)
            {
                CommandSet.SetStartAMU.Parameter = StartAMU;
                CommandSet.SetEndAMU.Parameter = GapStartAMU;
            }
            else
            {
                CommandSet.SetStartAMU.Parameter = GapEndAMU;
                CommandSet.SetEndAMU.Parameter = EndAMU;
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            CancellationRequested = true;
            e.Cancel = true;
        }

        #endregion

        #region Mass Spectrum

        private static void InitBackgroundRemoval()
        {
            try
            {
                string p = Path.Combine(Configuration.WorkingDirectory, Config.BackgroundFolderName);
                if (!Directory.Exists(p)) return;
                var f = Directory.GetFiles(p, Config.BackgroundSearchPattern);
                if (f.Length > 0)
                {
                    var buf = new Dictionary<double, List<double>>();
                    foreach (var item in f)
                    {
                        using TextReader r = new StreamReader(item);
                        using CsvReader cr = new CsvReader(r, Configuration.CsvConfig);
                        cr.Read();
                        cr.ReadHeader();
                        while (cr.Read())
                        {
                            try
                            {
                                double amu = cr.GetField<double>(0);
                                double val = cr.GetField<double>(1);
                                if (!buf.ContainsKey(amu))
                                {
                                    buf.Add(amu, new List<double>(f.Length));
                                }
                                buf[amu].Add(val);
                            }
                            catch (Exception ex)
                            {
                                Log("Can't parse background line:", ex);
                            }
                        }
                    }
                    Background = buf.ToDictionary(x => x.Key, x => x.Value.Average());
                }
            }
            catch (Exception ex)
            {
                Log("Can't parse background:", ex);
                Background = new Dictionary<double, double>();
            }
        }

        private static void InitDevice(string port)
        {
            Device = new Head(new Port(new RJCP.IO.Ports.SerialPortStream(port)));
            Device.TerminalLog += (s, t) => { Console.WriteLine(t); };
            Device.ScanCompleted += Device_ScanCompleted;
            Device.ExceptionLog += (s, e) => { Console.WriteLine(e.LogString); };
        }

        private static void Device_ScanCompleted(object sender, EventArgs e)
        {
            Console.WriteLine("\nScan completed.");
            int l = Device.LastScanResult.Length - 1;
            double totalPressure = Device.LastScanResult[l];
            var y = Device.LastScanResult.SkipLast(1).Select(x => x / (double)Device.CdemGain);
            y = totalPressure == 0 ? y.Select(x => x / 10000.0) : y.Select(x => x / totalPressure);
            double increment = 1.0 / Device.PointsPerAMU;
            var now = string.Format(Config.FileNameFormat, DateTime.Now);
            var t = new Thread(() =>
            {
                using TextWriter tw = new StreamWriter(Path.Combine(Configuration.WorkingDirectory, now));
                using CsvWriter cw = new CsvWriter(tw, Configuration.CsvConfig);
                cw.NextRecord();
                cw.NextRecord();
                double x = Device.StartAMU;
                foreach (var item in y)
                {
                    double v = item - (Background.ContainsKey(x) ? Background[x] : 0);
                    cw.WriteField(x.ToString(Config.AMUForamt, CultureInfo.InvariantCulture));
                    cw.WriteField(v.ToString(Config.IntensityFormat, CultureInfo.InvariantCulture));
                    x += increment;
                    cw.NextRecord();
                }
            });
            t.Start();
            t = new Thread(() =>
            {
                string p = Path.Combine(Configuration.WorkingDirectory, Config.BackupSubfolderName, now);
                using TextWriter tw = new StreamWriter(p);
                using CsvWriter cw = new CsvWriter(tw, Configuration.CsvConfig);
                cw.NextRecord();
                cw.NextRecord();
                double x = Device.StartAMU;
                for (int i = 0; i < Device.LastScanResult.Length; i++)
                {
                    cw.WriteField(x.ToString(Config.AMUForamt, CultureInfo.InvariantCulture));
                    cw.WriteField(Device.LastScanResult[i].ToString(Config.IntensityFormat, CultureInfo.InvariantCulture));
                    x += increment;
                    cw.NextRecord();
                }
            });
            t.Start();
        }

        static void StateMachine()
        {
            if (CommandSet.Sequences.ContainsKey(Device.State)) //No keys for states like Scanning that have to be awaited
                Device.ExecuteSequence(CommandSet.Sequences[Device.State]);
#if DEBUG
            Console.WriteLine("Resulting state: " + Enum.GetName(typeof(HeadState), Device.State));
#endif
        }

        #endregion

        #region Temperature and Gases

        private static void InitPipe(string name)
        {
            Pipe.GasNames = Config.GasNames;
            Pipe.GasGpioOffset = Config.GasGpioOffset;
            Pipe.UVGpioIndex = Config.UVGpioIndex;
            Pipe.LogEvent += (x, y) => { Log("Pipe message received: " + y); };
            Pipe.TemperatureReceived += Pipe_TemperatureReceived;
            Pipe.UVStateReceived += Pipe_UVStateReceived;
            Pipe.GasStateReceived += Pipe_GasStateReceived;
            Pipe.Initialize(name);
        }

        private static void Pipe_GasStateReceived(object sender, string e)
        {
            AppendLine(Config.GasFileName, e);
        }

        private static void Pipe_UVStateReceived(object sender, bool e)
        {
            AppendLine(Config.UVFileName, e.ToString(CultureInfo.InvariantCulture));
        }

        private static void Pipe_TemperatureReceived(object sender, float e)
        {
            AppendLine(Config.TemperatureFileName, 
                e.ToString(Config.TemperatureFormat, CultureInfo.InvariantCulture));
        }

        private static void AppendLine(string fileName, string payload)
        {
            var t = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var p = Path.Combine(
                        Configuration.WorkingDirectory,
                        Config.InfoSubfolderName,
                        fileName);
                    int retry = 3;
                    FileStream s = null;
                    while (retry-- > 0)
                    {
                        try
                        {
                            s = new FileStream(p, FileMode.Append, FileAccess.Write, FileShare.Read);
                            break;
                        }
                        catch (IOException ex)
                        {
                            Log("Warning: IOException encountered for an info file.", ex);
                        }
                    }
                    using (TextWriter w = new StreamWriter(s))
                    {
                        w.WriteLine(Config.InfoLineFormat, t, payload);
                    }
                    s.Dispose();
                }
                catch (Exception ex)
                {
                    Log("ERROR: Can't append an info file!", ex);
                }
            });
        }

        #endregion
    }
}
