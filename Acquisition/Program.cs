using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Linq;

namespace Acquisition
{
    public class Program
    {
        public static string BackupSubfolderName { get; set; } = "backup";
        public static string FileNameFormat { get; set; } = "Scan_{0:yyyy-MM-dd_HH-mm-ss}.csv";
        public static string AMUForamt { get; set; } = "F2";
        public static string IntensityFormat { get; set; } = "F4";

        private static bool CancellationRequested = false;
        private static string StartAMU = "1";
        private static string EndAMU = "65";

        public static string GapStartAMU { get; private set; } = null;
        public static string GapEndAMU { get; private set; } = null;

        public static Head Device { get; private set; }

        public static string WorkingDirectory { get => Environment.CurrentDirectory; }

        public static CsvConfiguration Configuration = new CsvConfiguration(CultureInfo.InvariantCulture);

        static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            Console.WriteLine("RGA Acquisition Helper v1.0 started!");

            Device = new Head(new Port(new RJCP.IO.Ports.SerialPortStream(args[0])));
            Device.TerminalLog += (s, t) => { Console.WriteLine(t); };
            Device.ScanCompleted += Device_ScanCompleted;
            Device.ExceptionLog += (s, e) => { Console.WriteLine(e.LogString); };

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

        private static void Device_ScanCompleted(object sender, EventArgs e)
        {
            Console.WriteLine("\nScan completed.");
            int l = Device.LastScanResult.Length - 1;
            double totalPressure = Device.LastScanResult[l];
            var y = Device.LastScanResult.SkipLast(1).Select(x => x / (double)Device.CdemGain);
            y = totalPressure == 0 ? y.Select(x => x / 10000.0) : y.Select(x => x / totalPressure);
            double increment = 1.0 / Device.PointsPerAMU;
            var now = string.Format(FileNameFormat, DateTime.Now);
            var t = new Thread(() =>
            {
                using TextWriter tw = new StreamWriter(Path.Combine(WorkingDirectory, now));
                using CsvWriter cw = new CsvWriter(tw, Configuration);
                cw.NextRecord();
                cw.NextRecord();
                double x = Device.StartAMU;
                foreach (var item in y)
                {
                    cw.WriteField(x.ToString(AMUForamt, CultureInfo.InvariantCulture));
                    cw.WriteField(item.ToString(IntensityFormat, CultureInfo.InvariantCulture));
                    x += increment;
                    cw.NextRecord();
                }
            });
            t.Start();
            t = new Thread(() =>
            {
                using TextWriter tw = new StreamWriter(Path.Combine(WorkingDirectory, BackupSubfolderName, now));
                using CsvWriter cw = new CsvWriter(tw, Configuration);
                cw.NextRecord();
                cw.NextRecord();
                double x = Device.StartAMU;
                for (int i = 0; i < Device.LastScanResult.Length; i++)
                {
                    cw.WriteField(x.ToString(AMUForamt, CultureInfo.InvariantCulture));
                    cw.WriteField(Device.LastScanResult[i].ToString(IntensityFormat, CultureInfo.InvariantCulture));
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
    }
}
