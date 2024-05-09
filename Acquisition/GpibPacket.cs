using System;
using System.Globalization;

namespace Acquisition
{
    public class GpibPacket
    {
        public GpibPacket(string source)
        {
            string[] fields = source.Split(';');
            Timestamp = DateTime.Parse(fields[0], CultureInfo.InvariantCulture);
            fields = fields[1].Split(',');
            Voltage = float.Parse(fields[0], NumberStyles.Float | NumberStyles.AllowLeadingSign);
            Current = float.Parse(fields[1], NumberStyles.Float | NumberStyles.AllowLeadingSign);
            Resistance = float.Parse(fields[2], NumberStyles.Float | NumberStyles.AllowLeadingSign);
            InstrumentTime = float.Parse(fields[3], NumberStyles.Float | NumberStyles.AllowLeadingSign);
            Status = float.Parse(fields[4], NumberStyles.Float | NumberStyles.AllowLeadingSign);
        }

        public DateTime Timestamp { get; set; }
        public float Voltage { get; set; }
        public float Current { get; set; }
        public float Resistance { get; set; }
        public float InstrumentTime { get; set; }
        public float Status { get; set; }
    }
}