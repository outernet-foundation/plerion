using System;

namespace Plerion
{
    [Serializable]
    public class WifiAccessPoint
    {
        public string macAddress;
        public int signalStrength;
        public int SignalToNoiseRatio = 0;
        public int channel;
    }
}