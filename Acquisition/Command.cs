using System;
using System.Collections.Generic;
using System.Text;

namespace Acquisition
{
    public delegate bool CommandExecutionEventHandler(Command cmd, Head sender, string response);

    public class Command
    {
        public Command(string name)
        {
            Name = name ?? throw new ArgumentNullException();
        }

        public Command(string name, CommandExecutionEventHandler callback, object param = null, bool noResp = false)
            : this(name)
        {
            Parameter = param?.ToString();
            ExecutionCallback = callback;
            NoResponseExpected = noResp;
        }

        public string Name { get; }

        public string Parameter { get; set; }

        public CommandExecutionEventHandler ExecutionCallback { get; set; }

        public bool Completed { get; private set; } = false;

        public bool Success { get; private set; } = false;

        public bool NoResponseExpected { get; } = false;

        public override string ToString()
        {
            return Name + (Parameter ?? "");
        }

        public void InvokeExecutionCallback(Head h, string resp)
        {
            try
            {
                Success = ExecutionCallback?.Invoke(this, h, resp) ?? true;
            }
            catch (Exception)
            {
                Success = false;
            }
            finally
            {
                Completed = true;
            }
        }

        public void ResetState()
        {
            Completed = false;
            Success = false;
        }
    }
}
