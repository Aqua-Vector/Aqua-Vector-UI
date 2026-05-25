using System.Collections.Generic;

namespace AquaVectorUI.models
{
    public class LidarScanObject
    {
        public List<ObstaclePoint> Points { get; set; } = new();
    }

    public class LidarScanPacket
    {
        public ushort  Seq     { get; set; }
        public Torpedo Torpedo { get; set; } = new();
        public List<LidarScanObject> Objects { get; set; } = new();
    }
}
