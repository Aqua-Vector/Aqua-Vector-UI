using System;
using AquaVectorUI.services;

namespace AquaVectorUI.models
{
    public class TargetObject
    {
        public double StopX { get; set; }
        public double X          { get; set; }
        public double Y          { get; set; }
        public double HeadingDeg { get; set; }
        public double SpeedMs    { get; set; }

        public TargetObject()
        {
            StopX      = EnvConfig.GetDouble("TARGET_STOP_X", 1.5);
            X          = EnvConfig.GetDouble("TARGET_START_X",      -1.0);
            Y          = EnvConfig.GetDouble("TARGET_START_Y",       3.0);
            HeadingDeg = EnvConfig.GetDouble("TARGET_HEADING_DEG",  90.0);
            SpeedMs    = EnvConfig.GetDouble("TARGET_SPEED_MS",      0.10);
        }

        // X축으로만 이동, StopX 도달 시 정지.
        public void Advance(double deltaTimeSec)
        {
            if (X >= StopX) return;

            X += SpeedMs * deltaTimeSec;
            if (X > StopX) X = StopX;
        }
    }
}
