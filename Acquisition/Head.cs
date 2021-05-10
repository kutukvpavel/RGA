using System;
using System.Collections.Generic;
using System.Threading;

namespace Acquisition
{
    public enum HeadState
    {
        PowerUp,
        Initialized,
        IonizerON,
        DetectorON,
        StartScan,
        Scanning,
        PowerDown
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
        public event EventHandler ScanCompleted;
        public object SynchronizingObject { get; } = new object();

        public Head(Port port)
        {
            CommunicationPort = port;
            CommunicationPort.SerialPort.ReceivedBytesThreshold = 2;
            CommunicationPort.SerialPort.ReadTimeout = 1000;
            CommunicationPort.SerialPort.ReadBufferSize = 64 * 1024;
            CommunicationPort.LineReceived += CommunicationPort_LineReceived;
            CommunicationPort.LineReceived += (s, e) => { if (State != HeadState.Scanning) TerminalLog?.Invoke(s, e); };
            CommunicationPort.BytesReceived += CommunicationPort_BytesReceived;
            CommunicationPort.CommandTransmitted += (s, e) => { TerminalLog?.Invoke(s, e); };
            CommunicationPort.SerialPort.Open();
        }

        public void Dispose()
        {
            if (_Disposed) return;
            try
            {
                ExecuteSequence(CommandSet.Sequences[HeadState.PowerDown]);
            }
            catch (Exception)
            {

            }
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
        private Queue<byte> _ScanBuffer = new Queue<byte>(4 * 2);
        private List<int> _ScanResult = new List<int>(65 * 10);

        private void CommunicationPort_BytesReceived(object sender, byte[] e)
        {
            if (State != HeadState.Scanning) return;
            try
            {
                lock (SynchronizingObject)
                {
                    foreach (var item in e)
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
                        if (_ScanResult.Count == TotalScanPoints)
                        {
                            _ScanBuffer.Clear();
                            break;
                        }
                    }
                    if (_ScanResult.Count >= TotalScanPoints)
                    {
                        LastScanResult = _ScanResult.ToArray();
                        _ScanResult.Clear();
                        State = HeadState.DetectorON;
                    }
                }
                if (State != HeadState.Scanning) ScanCompleted?.Invoke(this, new EventArgs());
            }
            catch (Exception ex)
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex));
            }
        }

        private void CommunicationPort_LineReceived(object sender, string e)
        {
            if (State == HeadState.Scanning) return;
            try
            {
                LastCommand.InvokeExecutionCallback(this, e);
            }
            catch (Exception ex)
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex, e));
            }
        }

        #endregion

        #region Properties

        public int Timeout { get; set; } = 5000;

        public string ID { get; private set; }

        private HeadState _State = HeadState.PowerUp;
        public HeadState State { get => _State; 
            private set 
            {
                _State = value;
                CommunicationPort.Mode = _State == HeadState.Scanning ? PortMode.Bytes : PortMode.String;
            }
        }

        public Port CommunicationPort { get; }

        public bool Busy { get => !(LastCommand?.Completed ?? true); }

        public Command LastCommand { get; private set; }

        public HeadStatusBits LastStatus { get; private set; } = HeadStatusBits.None;

        public int[] LastScanResult { get; private set; }

        public int StartAMU { get; private set; } = 1;

        public int EndAMU { get; private set; } = 200;

        public int PointsPerAMU { get; private set; } = 10;

        public int TotalScanPoints { get; private set; }

        public int CdemGain { get; private set; } = 1;

        #endregion

        #region Public Methods

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

        public void SetCdemGain(int g)
        {
            CdemGain = g;
        }

        public void SetTotalScanPoints(int p)
        {
            TotalScanPoints = p + 1; // + Total Pressure
        }

        public void SetID(string id)
        {
            ID = id;
        }

        public void SetStartAMU(int amu)
        {
            StartAMU = amu;
        }

        public void SetEndAMU(int amu)
        {
            EndAMU = amu;
        }

        public void SetPointsPerAMU(int p)
        {
            PointsPerAMU = p;
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
#if DEBUG_STEP
                    Console.WriteLine("Confirm execution of the next command...");
                    Console.ReadKey();
#endif
                    try
                    {
                        item.ResetState();
                        LastCommand = item;
                        CommunicationPort.Send(item);
                        if (item.NoResponseExpected)
                        {
                            item.InvokeExecutionCallback(this, null);
                            continue;
                        }
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

        #endregion
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
