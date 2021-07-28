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
        public static int UVGpioIndex { get; set; } = 2;
        public static int GasGpioOffset { get; set; } = 5;
        public static Dictionary<int, string> GasNames { get; set; } = new Dictionary<int, string>()
        {
            { 0, "Example 0" },
            { 1, "Example 1" }
        };

        public event EventHandler<float> TemperatureReceived;
        public event EventHandler<bool> UVStateReceived;
        public event EventHandler<string> GasStateReceived; 

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
                _Client.ServerMessage += _Client_ServerMessage;
                return true;
            }
            catch (Exception ex)
            {
                Program.Log("Unable to initialize named pipe client:", ex);
                return false;
            }
        }

        private void _Client_ServerMessage(NamedPipeConnection<string, string> connection, string message)
        {
            try
            {
                char t = message[0];
                var v = message.Remove(0, 1);
                switch (t)
                {
                    case 'C':
                        var split = v.Split('\n').Select(x => x.Trim('\r', '\n')).ToArray();
                        UVStateReceived?.Invoke(this, split[1][UVGpioIndex] != '0');
                        int i = split[1].IndexOf('1', GasGpioOffset);
                        if (GasNames.ContainsKey(i)) GasStateReceived?.Invoke(this, GasNames[i]);
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
                Program.Log("Unable to parse a pipe message:", ex);
            }
        }
    }
}
