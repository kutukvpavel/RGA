using System;

namespace Acquisition
{
    public class MgaPacket
    {
        public MgaPacket() { }

        public DateTime Timestamp { get; set; }
        public int SensorIndex { get; set; }
        public float HeaterResistance { get; set; }
        public float Conductance { get; set; }
        public byte[] RawValue { get; set; }
    }
}
