using System;

namespace AquaVectorUI.models
{
    public class TargetObject
    {
        private const double StartX = -1.0;
        private const double StopX  =  1.5;

        public double X          { get; set; }
        public double Y          { get; set; }
        public double HeadingDeg { get; set; }
        public double SpeedMs    { get; set; }

        public TargetObject()
        {
            X          = StartX;
            Y          = 3;
            HeadingDeg = 90;   // +X 방향 고정
            SpeedMs    = 0.10;
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
