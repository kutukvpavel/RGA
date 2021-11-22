using NamedPipeWrapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Acquisition
{
    public class NamedPipeService
    {
        public static NamedPipeService Instance { get; } = new NamedPipeService();

        public Dictionary<int, string> GasNames { get; set; } = new Dictionary<int, string>();
        public int UVGpioIndex { get; set; } = 0;
        public int GasGpioOffset { get; set; } = 5;
        public bool ExcludeUnknownGasNames { get; set; } = false;

        public event EventHandler<float> TemperatureReceived;
        public event EventHandler<bool> UVStateReceived;
        public event EventHandler<string> GasStateReceived;
        public event EventHandler<string> LogEvent;
        public event EventHandler<ExceptionEventArgs> LogException;
        public event EventHandler<MgaPacket> MgaPacketReceived;

        private NamedPipeClient<string> _LabPidClient;
        private NamedPipeClient<string> _MgaClient;

        private NamedPipeService()
        {
            
        }

        ~NamedPipeService()
        {
            if (_LabPidClient != null) _LabPidClient.Stop();
            if (_MgaClient != null) _MgaClient.Stop();
        }

        public bool Initialize(string labPidPipeName, string mgaPipeName)
        {
            try
            {
                _LabPidClient = new NamedPipeClient<string>(labPidPipeName);
                _LabPidClient.Start();
                _LabPidClient.ServerMessage += LabPidClient_ServerMessage;
                if ((mgaPipeName?.Length ?? 0) > 0)
                {
                    _MgaClient = new NamedPipeClient<string>(labPidPipeName);
                    _MgaClient.Start();
                    _MgaClient.ServerMessage += MgaClient_ServerMessage;
                }
                return true;
            }
            catch (Exception ex)
            {
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Unable to initialize named pipe client."));
                return false;
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
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Can't parse MGA packet."));
            }
        }

        private void LabPidClient_ServerMessage(NamedPipeConnection<string, string> connection, string message)
        {
            try
            {
                LogEvent?.Invoke(this, message);
            }
            catch (Exception)
            { }
            try
            {
                char t = message[0];
                var v = message.Remove(0, 1);
                switch (t)
                {
                    case 'C':
                        var split = v.Split('\n').Select(x => x.Trim('\r', '\n').Replace(" ", "")).ToArray();
                        try
                        {
                            UVStateReceived?.Invoke(this, split[1][UVGpioIndex] != '0');
                        }
                        catch (Exception ex)
                        {
                            LogException?.Invoke(this, new ExceptionEventArgs(ex, "UVStateReceived handler threw an exception."));
                        }
                        int i = split[1].IndexOf('1', GasGpioOffset);
                        if (ExcludeUnknownGasNames)
                        {
                            if (GasNames.ContainsKey(i)) GasStateReceived?.Invoke(this, GasNames[i]);
                        }
                        else
                        {
                            GasStateReceived?.Invoke(this, GasNames.ContainsKey(i) ? GasNames[i] : $"Gas #{i}");
                        }
                        break;
                    case 'T':
                        TemperatureReceived?.Invoke(this, float.Parse(v));
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Unable to parse a pipe message."));
            }
        }
    }
}
