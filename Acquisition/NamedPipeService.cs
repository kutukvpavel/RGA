using NamedPipeWrapper;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace Acquisition
{
    public class NamedPipeService
    {
        public static NamedPipeService Instance { get; } = new NamedPipeService();
        public static string[] GasPriority { get; set; }
        public static int GasGpioOffset { get; set; }
        public static string NoGasLabel { get; set; }
        public static string UVGpioLabel { get; set; }

        public event EventHandler<float> TemperatureReceived;
        public event EventHandler<bool> UVStateReceived;
        public event EventHandler<string> GasStateReceived;
        public event EventHandler<string> LogEvent;
        public event EventHandler<ExceptionEventArgs> LogException;
        public event EventHandler<MgaPacket> MgaPacketReceived;
        public event EventHandler<GPIBServerPacket> GpibPacketReceived;

        private NamedPipeClient<string> _LabPidClient;
        private NamedPipeClient<string> _MgaClient;
        private NamedPipeClient<string> _GPIBClient;

        private NamedPipeService()
        {
            
        }

        ~NamedPipeService()
        {
            if (_LabPidClient != null) _LabPidClient.Stop();
            if (_MgaClient != null) _MgaClient.Stop();
            if (_GPIBClient != null) _GPIBClient.Stop();
        }

        public bool Initialize(string labPidPipeName, string mgaPipeName, string gpibPipeName)
        {
            try
            {
                _LabPidClient = new NamedPipeClient<string>(labPidPipeName);
                _LabPidClient.Start();
                _LabPidClient.ServerMessage += LabPidClient_ServerMessage;
                if ((mgaPipeName?.Length ?? 0) > 0)
                {
                    _MgaClient = new NamedPipeClient<string>(mgaPipeName);
                    _MgaClient.Start();
                    _MgaClient.ServerMessage += MgaClient_ServerMessage;
                }
                if ((gpibPipeName?.Length ?? 0) > 0)
                {
                    _GPIBClient = new NamedPipeClient<string>(gpibPipeName);
                    _GPIBClient.AutoReconnect = true;
                    _GPIBClient.Start();
                    _GPIBClient.ServerMessage += GPIBClient_ServerMessage;
                }
                return true;
            }
            catch (Exception ex)
            {
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Unable to initialize named pipe client."));
                return false;
            }
        }

        private void GPIBClient_ServerMessage(NamedPipeConnection<string, string> connection, string message)
        {
            try
            {
                var packet = JsonConvert.DeserializeObject<GPIBServerPacket>(message);
                if (packet?.Response != null && packet?.InstrumentName != null) GpibPacketReceived?.Invoke(this, packet);
                else LogEvent?.Invoke(this, "Warning: GPIB message was empty");
            }
            catch (Exception ex)
            {
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Can't parse GPIB packet."));
            }
        }

        private void MgaClient_ServerMessage(NamedPipeConnection<string, string> connection, string message)
        {
            try
            {
                var packet = JsonConvert.DeserializeObject<MgaPacket>(message);
                if (packet != null) MgaPacketReceived?.Invoke(this, packet);
            }
            catch (Exception ex)
            {
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Can't parse an MGA packet."));
            }
        }

        private void LabPidClient_ServerMessage(NamedPipeConnection<string, string> connection, string message)
        {
            try
            {
                var p = JsonConvert.DeserializeObject<LabPidPacket>(message);
                var activeLabeledOutputs = p.Gpio.GetActiveOutputs();
                LogEvent?.Invoke(this, $"Pipe message: {p}");
                UVStateReceived?.Invoke(this, activeLabeledOutputs.Any(x => (x.Key < GasGpioOffset) && (x.Value == UVGpioLabel)));
                TemperatureReceived?.Invoke(this, p.Temperature);
                GasStateReceived?.Invoke(this, 
                    GasPriority.FirstOrDefault(x => activeLabeledOutputs.Any(y => (y.Key >= GasGpioOffset) && (y.Value == x))) 
                    ?? NoGasLabel);
            }
            catch (Exception ex)
            {
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Unable to parse a pipe message."));
            }
        }
    }
}
