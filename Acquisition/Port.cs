using System;
using System.Collections.Generic;
using System.Text;
using RJCP.IO.Ports;

namespace Acquisition
{
    public enum PortMode
    {
        Bytes,
        String
    }

    public class Port
    {
        public static readonly string NewLine = "\r";

        public event EventHandler<string> LineReceived;
        public event EventHandler<byte[]> BytesReceived;
        public event EventHandler<string> CommandTransmitted;

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
        public PortMode Mode { get; set; } = PortMode.String;

        public void Send(Command cmd)
        {
            while (!SerialPort.CtsHolding) ;
            var c = cmd.ToString();
            CommandTransmitted?.Invoke(this, c);
            SerialPort.Write(c);
            SerialPort.Write(NewLine);
            SerialPort.Write(NewLine);
        }

        private readonly StringBuilder Buffer = new StringBuilder();
        private readonly BlockingQueue _LineQueue = new BlockingQueue();
        private readonly BlockingQueue _ByteQueue = new BlockingQueue();

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            switch (Mode)
            {
                case PortMode.Bytes:
                    if (e.EventType == SerialData.NoData) return;
                    byte[] b = new byte[SerialPort.BytesToRead];
                    SerialPort.Read(b, 0, b.Length);
                    _ByteQueue.Enqueue(() => { BytesReceived?.Invoke(this, b); });
                    break;
                case PortMode.String:
                    if (e.EventType != SerialData.Chars) return;
                    string append = SerialPort.ReadExisting();
                    int index = append.IndexOf(NewLine);
                    if (index > -1)
                    {
                        append = Buffer.Append(append).ToString();
                        while (index > -1)
                        {
                            var invoke = append.Substring(0, index).TrimEnd(ToTrim);
                            _LineQueue.Enqueue(() => LineReceived?.Invoke(this, invoke));
                            append = append.Remove(0, index + 1);
                            index = append.IndexOf(NewLine);
                        }
                        Buffer.Clear();
                    }
                    Buffer.Append(append);
                    break;
                default:
                    throw new InvalidOperationException("Invalid port mode.");
            }
        }
    }
}
