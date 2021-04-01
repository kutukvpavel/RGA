using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;
using RJCP.IO.Ports;

namespace Acquisition
{
    public enum HeadState
    {
        PowerUp,
        Initialized,
        IonizerON,
        DetectorON,
        StartScan,
        Scanning
    }

    [Flags]
    public enum HeadStatusBits : byte
    {
        None = 0,
        Communication = (1 << 0),
        Filament = (1 << 1),
        ElectronMultiplier = (1 << 3),
        QuadrupoleMassFilter = (1 << 4),
        Electrometer = (1 << 5),
        ExternalPowerSupply = (1 << 6)
    }

    public class Head : IDisposable
    {
        public event EventHandler<ExceptionEventArgs> ExceptionLog;
        public event EventHandler<string> TerminalLog;
        public event EventHandler<List<int>> ScanCompleted;
        public object SynchronizingObject { get; } = new object();

        public Head(Port port)
        {
            CommunicationPort = port;
            CommunicationPort.LineReceived += (s, e) => { TerminalLog?.Invoke(s, e.Text); };
            CommunicationPort.LineReceived += CommunicationPort_LineReceived;
            CommunicationPort.CommandTransmitted += (s, e) => { TerminalLog?.Invoke(s, e.Text); };
            CommunicationPort.SerialPort.DataReceived += SerialPort_DataReceived;
            CommunicationPort.SerialPort.Open();
        }

        public void Dispose()
        {
            if (_Disposed) return;
            try
            {
                CommunicationPort.SerialPort.Close();
                CommunicationPort.SerialPort.Dispose();
            }
            catch (ObjectDisposedException)
            {

            }
            finally
            {
                _Disposed = true;
            }
        }

        #region Private

        private bool _Disposed = false;
        private BlockingQueue _TerminalQueue = new BlockingQueue();
        private BlockingQueue _ScanCompletedQueue = new BlockingQueue();
        private Queue<byte> _ScanBuffer = new Queue<byte>(4 * 2);
        private List<int> _ScanResult = new List<int>(65 * 10);

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (State != HeadState.Scanning) return;
            try
            {
                lock (SynchronizingObject)
                {
                    byte[] b = Encoding.ASCII.GetBytes(((SerialPortStream)sender).ReadExisting());
                    foreach (var item in b)
                    {
                        _ScanBuffer.Enqueue(item);
                    }
                    int len = _ScanBuffer.Count / 4;
                    for (int i = 0; i < len; i++)
                    {
                        int res = 0;
                        for (int j = 0; j < 4; j++)
                        {
                            res += _ScanBuffer.Dequeue() << (8 * j);
                        }
                        _ScanResult.Add(res);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex, ))
            }
        }

        private void CommunicationPort_LineReceived(object sender, TextEventArgs e)
        {
            if (State == HeadState.Scanning) return;
            try
            {
                LastCommand.InvokeExecutionCallback(this, e.Text);
            }
            catch (Exception ex)
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex, e.Text));
            }
        }

        #endregion

        public int Timeout { get; set; } = 5000;

        public string ID { get; private set; }

        public HeadState State { get; private set; } = HeadState.PowerUp;

        public Port CommunicationPort { get; }

        public bool Busy { get => !(LastCommand?.Completed ?? true); }

        public Command LastCommand { get; private set; }

        public HeadStatusBits LastStatus { get; private set; } = HeadStatusBits.None;

        public void StartScan()
        {
            lock (SynchronizingObject)
            {
                State = HeadState.StartScan;
            }
        }

        public void AbortScan()
        {
            lock (SynchronizingObject)
            {
                CommunicationPort.Send(CommandSet.ShutDownMassFilter);
                Thread.Sleep(1000);
                State = HeadState.DetectorON;
            }
        }

        public void SetID(string id)
        {
            ID = id;
        }

        public void SetStatus(HeadStatusBits s)
        {
            LastStatus = s;
        }

        public void ExecuteSequence(CommandSequence s)
        {
            lock (SynchronizingObject)
            {
                foreach (var item in s)
                {
#if DEBUG
                    Console.WriteLine("Confirm execution of the next command...");
                    Console.ReadKey();
#endif
                    try
                    {
                        item.ResetState();
                        LastCommand = item;
                        CommunicationPort.Send(item);
                        if (item.NoResponseExpected) continue;
                        for (int j = 0; j < Timeout; j++)
                        {
                            Thread.Sleep(1);
                            if (item.Completed) break;
                        }
                        if (!item.Success)
                        {
                            State = s.ErrorResetState;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex));
                        State = s.ErrorResetState;
                        return;
                    }
                }
                State = s.SuccessNextState;
            }
        }
    }

    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception ex, string data = null)
        {
            Exception = ex;
            Data = data;
        }

        public Exception Exception { get; }
        public string Data { get; }
        public string LogString
        {
            get
            {
                return Data != null ? $"{Exception.Message}\nData: {Data}" : Exception.Message;
            }
        }
    }
}
