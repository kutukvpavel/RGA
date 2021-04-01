using System;
using System.Collections.Generic;

namespace Acquisition
{
    public class Program
    {
        public static Head Device { get; private set; }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            Device = new Head(new Port(new RJCP.IO.Ports.SerialPortStream(args[0])));
            Device.TerminalLog += (s, t) => { Console.WriteLine(t); };

            try
            {
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
            }
            finally
            {
                Device.Dispose();
            }
        }

        static void StateMachine()
        {
            Device.ExecuteSequence(CommandSet.Sequences[Device.State]);
#if DEBUG
            Console.WriteLine("Resulting state: " + Enum.GetName(typeof(HeadState), Device.State));
#endif
        }
    }
}
