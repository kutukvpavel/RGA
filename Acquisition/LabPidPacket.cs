using System.Collections.Generic;
using System.Linq;

namespace Acquisition
{
    public class LabPidPacket
    {
        public float Temperature { get; set; }
        public float Setpoint { get; set; }
        public GpioDescriptor Gpio { get; set; }

        public override string ToString()
        {
            return @$"T={Temperature:F1}, S={Setpoint:F1},
Active GPIO=({Gpio})";
        }
    }

    public class GpioDescriptor
    {
        public bool[] Inputs { get; set; }
        public bool[] Outputs { get; set; }

        public Dictionary<int, string> InputLabels { get; set; }
        public Dictionary<int, string> OutputLabels { get; set; }

        public IEnumerable<KeyValuePair<int, string>> GetActiveOutputs()
        {
            return OutputLabels.Where(x => Outputs[x.Key]);
        }
        public IEnumerable<KeyValuePair<int, string>> GetActiveInputs()
        {
            return InputLabels.Where(x => Inputs[x.Key]);
        }
        public override string ToString()
        {
            return $"I={GetActiveInputs().Select(x => x.Value)}, O={GetActiveOutputs().Select(x => x.Value)}";
        }
    }
}
