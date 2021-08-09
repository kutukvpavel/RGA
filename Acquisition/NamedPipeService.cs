using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NamedPipeWrapper;

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

        private NamedPipeClient<string> _Client;

        private NamedPipeService()
        {
            
        }

        ~NamedPipeService()
        {
            if (_Client != null) _Client.Stop();
        }

        public bool Initialize(string pipeName)
        {
            try
            {
                _Client = new NamedPipeClient<string>(pipeName);
                _Client.Start();
                _Client.ServerMessage += Client_ServerMessage;
                return true;
            }
            catch (Exception ex)
            {
                LogException?.Invoke(this, new ExceptionEventArgs(ex, "Unable to initialize named pipe client."));
                return false;
            }
        }

        private void Client_ServerMessage(NamedPipeConnection<string, string> connection, string message)
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
