using CsvHelper;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Acquisition
{
    public class Program
    {
        private static bool CancellationRequested = false;
        private static string StartAMU = "1";
        private static string EndAMU = "65";
        private static NamedPipeService Pipe = NamedPipeService.Instance;

        public static string GapStartAMU { get; private set; } = null;
        public static string GapEndAMU { get; private set; } = null;
        public static Head Device { get; private set; }

        static void Main(string[] args)
        {
            //Init
            Console.CancelKeyPress += Console_CancelKeyPress;
            InitDevice(args[0]);
            InitPipe(Configuration.PipeName);
            VerifyDirectoryExists(Configuration.WorkingDirectory);
            VerifyDirectoryExists(Configuration.WorkingDirectory, Configuration.BackupSubfolderName);
            VerifyDirectoryExists(Configuration.WorkingDirectory, Configuration.InfoSubfolderName);
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
            var now = string.Format(Configuration.FileNameFormat, DateTime.Now);
            var t = new Thread(() =>
            {
                using TextWriter tw = new StreamWriter(Path.Combine(Configuration.WorkingDirectory, now));
                using CsvWriter cw = new CsvWriter(tw, Configuration.CsvConfig);
                cw.NextRecord();
                cw.NextRecord();
                double x = Device.StartAMU;
                foreach (var item in y)
                {
                    cw.WriteField(x.ToString(Configuration.AMUForamt, CultureInfo.InvariantCulture));
                    cw.WriteField(item.ToString(Configuration.IntensityFormat, CultureInfo.InvariantCulture));
                    x += increment;
                    cw.NextRecord();
                }
            });
            t.Start();
            t = new Thread(() =>
            {
                string p = Path.Combine(Configuration.WorkingDirectory, Configuration.BackupSubfolderName, now);
                using TextWriter tw = new StreamWriter(p);
                using CsvWriter cw = new CsvWriter(tw, Configuration.CsvConfig);
                cw.NextRecord();
                cw.NextRecord();
                double x = Device.StartAMU;
                for (int i = 0; i < Device.LastScanResult.Length; i++)
                {
                    cw.WriteField(x.ToString(Configuration.AMUForamt, CultureInfo.InvariantCulture));
                    cw.WriteField(Device.LastScanResult[i].ToString(Configuration.IntensityFormat, CultureInfo.InvariantCulture));
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
            Pipe.TemperatureReceived += Pipe_TemperatureReceived;
            Pipe.UVStateReceived += Pipe_UVStateReceived;
            Pipe.GasStateReceived += Pipe_GasStateReceived;
            Pipe.Initialize(name);
        }

        private static void Pipe_GasStateReceived(object sender, string e)
        {
            AppendLine(Configuration.GasFileName, e);
        }

        private static void Pipe_UVStateReceived(object sender, bool e)
        {
            AppendLine(Configuration.UVFileName, e.ToString(CultureInfo.InvariantCulture));
        }

        private static void Pipe_TemperatureReceived(object sender, float e)
        {
            AppendLine(Configuration.TemperatureFileName, 
                e.ToString(Configuration.TemperatureFormat, CultureInfo.InvariantCulture));
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
                        Configuration.InfoSubfolderName,
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
                        catch (IOException)
                        {
                            Console.WriteLine("Warning: IOException encountered for an info file.");
                        }
                    }
                    using TextWriter w = new StreamWriter(s);
                    w.WriteLine(Configuration.InfoLineFormat, t, payload);
                    s.Dispose();
                }
                catch (Exception)
                {
                    Console.WriteLine("ERROR: Can't append an info file!");
                }
            });
        }

        #endregion
    }
}
