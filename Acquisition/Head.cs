using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Timer = System.Timers.Timer;
using System.Threading.Tasks;

namespace Acquisition
{
    public enum HeadState
    {
        PowerUp,
        Initialized,
        IonizerON,
        DetectorON,
        ReadyToScan,
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

        public Head(Port port, int scanTimeout)
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
            _ScanTimeoutTimer.Interval = scanTimeout;
            _ScanTimeoutTimer.Elapsed += ScanTimeoutTimer_Elapsed;
        }

        public void Dispose()
        {
            if (_Disposed) return;
            try
            {
                ExecuteSequence(CommandSet.Sequences[HeadState.PowerDown]);
            }
            catch (Exception ex)
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex));
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
        //private BlockingQueue _TerminalQueue = new BlockingQueue();
        private Queue<byte> _ScanBuffer = new Queue<byte>(4 * 2);
        private List<double> _ScanResult = new List<double>(65 * 10);
        private Timer _ScanTimeoutTimer = new Timer(20000) { AutoReset = false, Enabled = false };

        private void ScanTimeoutTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(SynchronizingObject, 10000))
            {
                try
                {
                    try
                    {
                        var eea = new ExceptionEventArgs(new TimeoutException("Scan timeout."),
                            $@"Current buffered bytes: {string.Join(' ', _ScanBuffer.Select(x => x.ToString("X2")))};
Current resuslts: {string.Join(' ', _ScanResult.Select(x => x.ToString()))};
Scan points: expected {TotalScanPoints}, current {_ScanResult.Count};
Port queue length: {CommunicationPort.SerialPort.BytesToRead} in, {CommunicationPort.SerialPort.BytesToWrite} out."
                        );
                        Task.Run(() =>
                        {
                            ExceptionLog?.Invoke(this, eea);
                        });
                    }
                    catch (Exception ex)
                    {
                        ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex, "Unable to save scan timeout log"));
                    }

                    CommunicationPort.DiscardBuffers();
                    _ScanBuffer.Clear();
                    _ScanResult.Clear();
                    State = HeadState.ReadyToScan;
                }
                catch (Exception ex)
                {
                    try
                    {
                        ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex, "Scan timeout handler emergency log"));
                    }
                    catch (Exception)
                    { }
                }
                finally
                {
                    Monitor.Exit(SynchronizingObject);
                }
            }
            else
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(new TimeoutException("Scan deadlock.")));
                State = HeadState.PowerDown;
            }
        }

        private void CommunicationPort_BytesReceived(object sender, byte[] e)
        {
            if (State != HeadState.Scanning) return;
            try
            {
                bool scanFinished = false;
                if (!Monitor.TryEnter(SynchronizingObject, 10000))
                {
                    throw new TimeoutException("Can't acquire a lock to process the bytes received.");
                }
                try
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
                        scanFinished = true;
                    }
                }
                finally
                {
                    Monitor.Exit(SynchronizingObject);
                }
                if (scanFinished)
                {
                    _ScanTimeoutTimer.Stop();
                    /**
                     * This handles synchronously copies current MovingAverage data,
                     * therefore AppMain can safely swap Gap buffers after this line.
                     * This handler also spawns two separate threads for IO operations,
                     * that run asynchronously.
                     * First one copies a reference to Device.LastScanResult,
                     * therefore Device can safely update that property.
                     * Second one uses a deep copy of MovingAverage data.
                     */
                    ScanCompleted?.Invoke(this, new EventArgs());
                    State = HeadState.ReadyToScan;
                }
            }
            catch (Exception ex)
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex));
                State = HeadState.ReadyToScan; //Reset scan without ScanCompleted invocation
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

        public double[] LastScanResult { get; private set; }

        public int StartAMU { get; private set; } = 1;

        public int EndAMU { get; private set; } = 200;

        public int PointsPerAMU { get; private set; } = 10;

        public int TotalScanPoints { get; private set; }

        public int CdemGain { get; private set; } = 1;

        #endregion

        #region Public Methods

        public void StartScan()
        {
            if (!Monitor.TryEnter(SynchronizingObject, 20000))
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(new TimeoutException(
                    "Can't acquire a lock to start a scan.")));
                return;
            }
            try
            {
                State = HeadState.StartScan;
                _ScanTimeoutTimer.Start();
            }
            finally
            {
                Monitor.Exit(SynchronizingObject);
            }
        }

        public void AbortScan()
        {
            _ScanTimeoutTimer.Stop();
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
            if (!Monitor.TryEnter(SynchronizingObject, 20000))
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(new TimeoutException(
                    "Can't acquire a lock to execute the sequence.")));
                return;
            }
            try
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
            finally
            {
                Monitor.Exit(SynchronizingObject);
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
                return Data != null ? $"{Exception}\nData: {Data}" : Exception.ToString();
            }
        }
    }
}
