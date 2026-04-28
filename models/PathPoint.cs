using System;

namespace AquaVectorUI.models
{
    public class PathPoint
    {
        public DateTime Timestamp { get; set; }
        public double WorldX { get; set; }
        public double WorldY { get; set; }
        public double HeadingDeg { get; set; }
    }
}
