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


        }

        static void StateMachine()
        {
            if (CommandSet.Sequences.ContainsKey(Device.State))
            {
                Device.ExecuteSequence(CommandSet.Sequences[Device.State]);
            }
        }
    }
}
