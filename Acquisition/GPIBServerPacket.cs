using System;

namespace Acquisition
{
    public class GPIBServerPacket
    {
        public GPIBServerPacket()
        { }

        public string TimeReceived;
        public string ControllerName;
        public string InstrumentName;
        public string Command;
        public string Response;

        public override string ToString()
        {
            return $"{ControllerName},{InstrumentName},{Response}";
        }
    }
}
