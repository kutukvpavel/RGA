using System;
using System.Collections.Generic;
using System.Text;
using RJCP.IO.Ports;

namespace Acquisition
{
    public class Port
    {
        public static readonly string NewLine = "\r";

        public event EventHandler<TextEventArgs> LineReceived;
        public event EventHandler<TextEventArgs> CommandTransmitted;

        public Port(SerialPortStream port)
        {
            SerialPort = port;
            port.RtsEnable = true;
            port.Encoding = Encoding.ASCII;
            port.BaudRate = 28800;
            port.StopBits = StopBits.One;
            port.Parity = Parity.None;
            port.DataReceived += Port_DataReceived;
        }

        public SerialPortStream SerialPort { get; }
        public char[] ToTrim { get; set; } = new char[] { '\n', '\r' };

        public void Send(Command cmd)
        {
            while (!SerialPort.CtsHolding) ;
            var c = cmd.ToString();
            CommandTransmitted?.Invoke(this, new TextEventArgs(c));
            SerialPort.Write(c);
            SerialPort.Write(NewLine);
            SerialPort.Write(NewLine);
        }

        private readonly StringBuilder Buffer = new StringBuilder();

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string append = SerialPort.ReadExisting();
            int index = append.IndexOf(NewLine);
            if (index > -1)
            {
                append = Buffer.Append(append).ToString();
                while (index > -1)
                {
                    LineReceived?.Invoke(this, new TextEventArgs(append.Substring(0, index).TrimEnd(ToTrim)));
                    append = append.Remove(0, index + 1);
                    index = append.IndexOf(NewLine);
                }
                Buffer.Clear();
            }
            Buffer.Append(append);
        }
    }

    public class TextEventArgs : EventArgs
    {
        public TextEventArgs(string txt)
        {
            Text = txt;
        }

        public string Text { get; }
    }
}
