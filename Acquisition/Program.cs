using CsvHelper;
using LLibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Acquisition
{
    public class Program
    {
        private const string BackupRestoreKey = "--restore";

        private static bool CancellationRequested = false;
        private static string StartAMU = "1";
        private static string EndAMU = "65";
        private static readonly NamedPipeService Pipe = NamedPipeService.Instance;
        private static readonly L Logger = new L();
        private static Configuration Config = new Configuration();
        private static MovingAverageContainer Average;
        private static MovingAverageContainer GapSwappedAverage;
        
        public static SpectrumBackground Background { get; private set; }
        public static string GapStartAMU { get; private set; } = null;
        public static string GapEndAMU { get; private set; } = null;
        public static Head Device { get; private set; }

        static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            if (args.Length == 0)
            {
                Log("No arguments. Shutting down.");
                return;
            }
            //Load Settings
            try
            {
                Config = Serializer.Deserialize(Serializer.SettingsFileName, Config);
                if (Config.MovingAverageWindowWidth < 1) Config.MovingAverageWindowWidth = 1;
            }
            catch (Exception ex)
            {
                Log("Can't deserilize settings file:", ex);
                Config = new Configuration();
            }
            try
            {
                if (args[0] == BackupRestoreKey)
                {
                    BackupRestoreMain(args);
                }
                else
                {
                    AcquisitionMain(args);
                }
            }
            catch (Exception ex)
            {
                Log("Error inside the main method.", ex);
            }
            finally
            {
                try
                {
                    Serializer.Serialize(Config, Serializer.SettingsFileName);
                }
                catch (Exception ex)
                {
                    Log("Failed to save configuration.", ex);
                }
            }
        }

        static void AcquisitionMain(string[] args)
        {
            Average = new MovingAverageContainer(Config.MovingAverageWindowWidth);
            //Init
            CommandSet.SetNoiseFloor.Parameter = Config.NoiseFloorSetting.ToString();
            InitBackgroundRemoval();
            InitDevice(args[0]);
            InitPipe(Config.LabPidPipeName, Config.MgaPipeName, Config.GpibPipeName);
            VerifyDirectoryExists(Configuration.WorkingDirectory);
            VerifyDirectoryExists(Configuration.WorkingDirectory, Config.BackupSubfolderName);
            VerifyDirectoryExists(Configuration.WorkingDirectory, Config.InfoSubfolderName);
            Console.WriteLine("RGA Acquisition Helper v1.0 started!");
            Log($"Background data found for {Background.Count} AMUs.");
            //CLI
            try
            {
                CommandSet.SetStartAMU.Parameter = args[1];
                CommandSet.SetEndAMU.Parameter = args[2];
                CommandSet.SetPointsPerAMU.Parameter = args[3];
                CommandSet.TurnHVON.Parameter = args[4];
                GapStartAMU = args[5];
                GapEndAMU = args[6];
            }
            catch (IndexOutOfRangeException)
            {

            }
            Config.GapEnabled = false;
            if (GapStartAMU != null && GapEndAMU != null)
            {
                Config.GapEnabled = true;
                StartAMU = args[1];
                EndAMU = args[2];
                GapSwappedAverage = new MovingAverageContainer(Average.Width);
            }
            //Acquisition cycle
            try
            {
                while (Device.State != HeadState.PowerDown)
                {
                    StateMachine();
                    Thread.Sleep(500);
                    if (Device.State == HeadState.ReadyToScan)
                    {
                        Console.WriteLine("Starting new scan...");
                        if (CancellationRequested) break;
                        if (Config.GapEnabled) ToggleAroundGap();
                        Device.StartScan();
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Fatal error in the acquisition loop.", ex);
            }
            finally
            {
                Config.CdemGain = Device.CdemGain;
                Device.Dispose();
            }
        }

        static void BackupRestoreMain(string[] args)
        {
            string target = null;
            string searchPattern = null;
            try
            {
                if (Directory.Exists(args[1]))
                {
                    target = args[1];
                }
                Console.WriteLine($"Will be saving into: {target}");
                searchPattern = args[2];
            }
            catch (IndexOutOfRangeException)
            {
                Log("Using default restore folder.", null);
            }
            catch (Exception ex)
            {
                Log("Using default restore folder due to an error.", ex);
            }
            if (target == null) target = BackupRestore.BackupData.DefaultRestoreLocation;
            if (searchPattern == null) searchPattern = "*.csv";
            Console.WriteLine("Loading backup data...");
            BackupRestore.BackupData backupData = new BackupRestore.BackupData(
                Path.Combine(Configuration.WorkingDirectory, Config.BackupSubfolderName))
            {
                CsvConfig = Configuration.CsvConfig
            };
            backupData.LogException += (s, e) => Log(e.LogString);
            backupData.Load(searchPattern);
            Console.WriteLine("Restoring...");
            backupData.SaveWith(target, Config);
            Console.WriteLine("Done.");
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
            MovingAverageContainer temp = Average;
            Average = GapSwappedAverage;
            GapSwappedAverage = temp;
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
                Background = SpectrumBackground.Load(
                    Path.Combine(Configuration.WorkingDirectory, Config.BackgroundFolderName),
                    Config.BackgroundSearchPattern, Config.BackgroundScaling);
            }
            catch (Exception ex)
            {
                Log("Can't parse background:", ex);
                Background = new SpectrumBackground();
            }
        }

        private static void InitDevice(string port)
        {
            Device = new Head(new Port(new RJCP.IO.Ports.SerialPortStream(port)), Config.ScanTimeout);
            if (Config.LogTerminalCommunication) Device.TerminalLog += (s, t) => Log(t);
            else Device.TerminalLog += (s, t) => Console.WriteLine(t);
            Device.ScanCompleted += Device_ScanCompleted;
            Device.ExceptionLog += (s, e) => Log(e.LogString);
        }

        private static void Device_ScanCompleted(object sender, EventArgs e)
        {
            var lastScanResult = Device.LastScanResult; //Copies a reference, intended
            Console.WriteLine("\nScan completed.");
            double increment = 1.0 / Device.PointsPerAMU;
            var now = string.Format(Config.FileNameFormat, DateTime.Now);
            //Save original data as a backup
            var t = new Thread(() =>
            {
                try
                {
                    string p = Path.Combine(Configuration.WorkingDirectory, Config.BackupSubfolderName, now);
                    using TextWriter tw = new StreamWriter(p);
                    using CsvWriter cw = new CsvWriter(tw, Configuration.CsvConfig);
                    cw.NextRecord();
                    cw.NextRecord();
                    double x = Device.StartAMU;
                    for (int i = 0; i < lastScanResult.Length; i++)
                    {
                        cw.WriteField(x.ToString(Config.AMUFormat, CultureInfo.InvariantCulture));
                        cw.WriteField(lastScanResult[i].ToString(Config.IntensityFormat, CultureInfo.InvariantCulture));
                        x += increment;
                        cw.NextRecord();
                    }
                }
                catch (Exception ex)
                {
                    Log("Failed to save a backup file.", ex);
                }
            });
            t.Start();
            //Calculate derived data (this section runs sychronously)
            IEnumerable<double> y;
            try
            {
                Average.Enqueue(lastScanResult);
                var a = Average.CurrentAverage; //Returns a deep copy
                if (a.Length != lastScanResult.Length)
                {
                    Log("Improper action of MovingAveragingContainer detected: output array length is not equal to the input array length.");
                    return;
                }
                for (int i = 0; i < a.Length; i++)
                {
                    a[i] /= Device.CdemGain;
                }
                y = a.SkipLast(1);
                double totalPressure = a.Last();
                y = totalPressure == 0 ? y.Select(x => x / Config.CdemEnabledAdditionalDivisionFactor) : y.Select(x => x / totalPressure);
            }
            catch (Exception ex)
            {
                Log("Error during data calculation.", ex);
                return;
            }
            //Save derived data
            t = new Thread(() =>
            {
                try
                {
                    using TextWriter tw = new StreamWriter(Path.Combine(Configuration.WorkingDirectory, now));
                    using CsvWriter cw = new CsvWriter(tw, Configuration.CsvConfig);
                    cw.NextRecord();
                    cw.NextRecord();
                    double x = Device.StartAMU;
                    foreach (var item in y)
                    {
                        double xBkg = Math.Round(x, Config.BackgroundAMURoundingDigits);
                        double v = item - (Background.ContainsKey(xBkg) ? Background[xBkg] : 0);
                        cw.WriteField(x.ToString(Config.AMUFormat, CultureInfo.InvariantCulture));
                        cw.WriteField(v.ToString(Config.IntensityFormat, CultureInfo.InvariantCulture));
                        x += increment;
                        cw.NextRecord();
                    }
                }
                catch (Exception ex)
                {
                    Log("Failed to save a processed spectrum file.", ex);
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

        #region Temperature, Gases and Sensors

        private static void InitPipe(string labPidName, string mgaName, string gpibPipe)
        {
            NamedPipeService.GasGpioOffset = Config.GasGpioOffset;
            NamedPipeService.GasPriority = Config.GasPriority;
            NamedPipeService.NoGasLabel = Config.NoGasLabel;
            NamedPipeService.UVGpioLabel = Config.UVGpioLabel;
            if (Config.LogPipeMessages) Pipe.LogEvent += (x, y) => { Log("Pipe message received: " + y); };
            else Pipe.LogEvent += (x, y) => Console.WriteLine(y);
            Pipe.LogException += (x, y) => { Log(y.LogString); };
            Pipe.TemperatureReceived += Pipe_TemperatureReceived;
            Pipe.UVStateReceived += Pipe_UVStateReceived;
            Pipe.GasStateReceived += Pipe_GasStateReceived;
            Pipe.MgaPacketReceived += Pipe_MgaPacketReceived;
            Pipe.GpibPacketReceived += Pipe_GpibPacketReceived;
            Pipe.Initialize(labPidName, mgaName, gpibPipe);
        }

        private static string _LastGas = string.Empty;
        private static void Pipe_GasStateReceived(object sender, string e)
        {
            if (_LastGas == e) return;
            AppendLine(Config.GasFileName, e);
            _LastGas = e;
        }

        private static bool _LastUV = false;
        private static void Pipe_UVStateReceived(object sender, bool e)
        {
            if (_LastUV == e) return;
            AppendLine(Config.UVFileName, e.ToString(CultureInfo.InvariantCulture));
            _LastUV = e;
        }

        private static float _LastTemperature = 0;
        private static void Pipe_TemperatureReceived(object sender, float e)
        {
            if (_LastTemperature == e) return;
            AppendLine(Config.TemperatureFileName, 
                e.ToString(Config.TemperatureFormat, CultureInfo.InvariantCulture));
            _LastTemperature = e;
        }

        private static void Pipe_MgaPacketReceived(object sender, MgaPacket e)
        {
            AppendLine(string.Format(Config.SensorFileName, e.SensorIndex), 
                e.Conductance.ToString(Config.SensorNumberFormat, CultureInfo.InvariantCulture));
        }
        private static void Pipe_GpibPacketReceived(object sender, GpibPacket e)
        {
            AppendLine(string.Format(Config.SensorFileName, 0), 
                e.Current.ToString(Config.SensorNumberFormat, CultureInfo.InvariantCulture));
        }

        private static List<Task> _PendingTasks = new List<Task>();
        private static void AppendLine(string fileName, string payload)
        {
            var t = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < _PendingTasks.Count; i++)
            {
                if (_PendingTasks[i].IsCompleted) _PendingTasks.RemoveAt(i--);
            }
            if (_PendingTasks.Count > Config.WriterThreadLimit)
            {
                Log("Writer thread limit reached!");
                return;
            }
            var task = Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Writing info: {payload}");
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
            _PendingTasks.Add(task);
        }

        #endregion
    }
}
