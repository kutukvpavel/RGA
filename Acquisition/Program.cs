﻿using System;
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
        private static bool CancellationRequested = false;

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

            CommandSet.SetEndAMU.Parameter = 65.ToString();

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
            var y = totalPressure == 0 ? Device.LastScanResult.SkipLast(1).Select(x => (double)x)
                : Device.LastScanResult.SkipLast(1).Select(x => x / totalPressure);
            double increment = 1.0 / Device.PointsPerAMU;
            double x = Device.StartAMU;
            var t = new Thread(() =>
            {
                using TextWriter tw = new StreamWriter(
                    Path.Combine(WorkingDirectory, $"Scan_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"));
                using CsvWriter cw = new CsvWriter(tw, Configuration);
                cw.NextRecord();
                cw.NextRecord();
                foreach (var item in y)
                {
                    cw.WriteField(x.ToString("F2", CultureInfo.InvariantCulture));
                    cw.WriteField(item.ToString("E4", CultureInfo.InvariantCulture));
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
