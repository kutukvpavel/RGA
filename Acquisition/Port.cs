using System;
using System.Collections.Generic;
using System.Text;
using RJCP.IO.Ports;

namespace Acquisition
{
    public class Port
    {
        public static readonly string NewLine = "\n";

        public event EventHandler<TextEventArgs> LineReceived;

        public Port(SerialPortStream port)
        {
            SerialPort = port;
            port.RtsEnable = true;
            port.DataReceived += Port_DataReceived;
        }

        public SerialPortStream SerialPort { get; }

        public void Send(Command cmd)
        {
            while (!SerialPort.CtsHolding) ;
            SerialPort.Write(cmd.ToString());
            SerialPort.Write(NewLine);
        }

        private StringBuilder Buffer = new StringBuilder();

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string append = SerialPort.ReadExisting();
            int index = append.IndexOf(NewLine);
            if (index > -1)
            {
                append = Buffer.Append(append).ToString();
                while (index > -1)
                {
                    LineReceived?.Invoke(this, new TextEventArgs(append.Substring(0, index)));
                    append.Remove(0, index + 1);
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
