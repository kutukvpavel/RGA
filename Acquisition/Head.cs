using System;
using System.Collections.Generic;
using System.Text;
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
        AbortScan
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

    public class Head
    {
        public event EventHandler<ExceptionEventArgs> ExceptionLog;

        public Head(Port port)
        {
            CommunicationPort = port;
            CommunicationPort.LineReceived += CommunicationPort_LineReceived;
        }

        private void CommunicationPort_LineReceived(object sender, TextEventArgs e)
        {
            try
            {
                switch (LastCommand.ExpectedResponse)
                {
                    case CommandExpectedResponse.StatusByte:
                        LastStatus = (HeadStatusBits)(Encoding.ASCII.GetBytes(e.Text)[0]);
                        LastCommand.Response = LastStatus;
                        break;
                    case CommandExpectedResponse.String:
                        LastCommand.Response = e.Text;
                        break;
                    case CommandExpectedResponse.Number:
                        LastCommand.Response = double.Parse(e.Text);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                ExceptionLog?.Invoke(this, new ExceptionEventArgs(ex, e.Text));
                LastCommand.Response = null;
                Busy = false;
            }
        }

        public HeadState State { get; private set; } = HeadState.PowerUp;

        public Port CommunicationPort { get; }

        public bool Busy { get; private set; } = false;

        public Command LastCommand { get; private set; }

        public HeadStatusBits LastStatus { get; private set; } = HeadStatusBits.None;

        public void ExecuteSequence(CommandSequence s)
        {
            foreach (var item in s)
            {
                Busy = true;
                LastCommand = item;
                LastCommand.Response = null;
                CommunicationPort.Send(item);
                while (Busy)
                {
                    Thread.Sleep(100);
                }
                if (LastCommand.Response == null)
                {
                    State = s.ErrorResetState;
                    return;
                }
            }
            State = s.SuccessNextState;
        }
    }

    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception ex, string data)
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
                return $"{Exception.Message}\nData: {Data}";
            }
        }
    }
}
