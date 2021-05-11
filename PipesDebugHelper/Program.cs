using System;
using NamedPipeWrapper;

namespace PipesDebugHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            NamedPipeClient<string> c = new NamedPipeClient<string>("LabPID_Profile_Broadcast");
            c.ServerMessage += C_ServerMessage;
            c.Start();
            c.WaitForConnection();
            c.WaitForDisconnection();
            c.Stop();
        }

        private static void C_ServerMessage(NamedPipeConnection<string, string> connection, string message)
        {
            Console.WriteLine(message);
        }
    }
}
