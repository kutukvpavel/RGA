using System;
using System.Collections.Generic;
using System.Text;

namespace Acquisition
{
    public enum CommandExpectedResponse
    {
        StatusByte,
        String,
        Number
    }

    public class Command
    {
        public Command(string name)
        {
            Name = name ?? throw new ArgumentNullException();
        }

        public Command(string name, object param) : this(name)
        {
            Parameter = param.ToString();
        }

        public string Name { get; }

        public string Parameter { get; set; }

        public CommandExpectedResponse ExpectedResponse { get; }

        public object Response { get; set; }

        public override string ToString()
        {
            return Name + (Parameter ?? "");
        }
    }
}
