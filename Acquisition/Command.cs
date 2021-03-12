using System;
using System.Collections.Generic;
using System.Text;

namespace Acquisition
{
    public class Command
    {
        public Command(string name)
        {
            if (name == null) throw new ArgumentNullException();
            Name = name;
        }

        public Command(string name, object param) : this(name)
        {
            Parameter = param.ToString();
        }

        public string Name { get; }

        public string Parameter { get; set; }

        public override string ToString()
        {
            return Name + (Parameter ?? "");
        }
    }
}
