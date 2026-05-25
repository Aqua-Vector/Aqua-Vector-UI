using System;

namespace AquaVectorUI.models
{
    public class TargetObject
    {
        private const double XMin = -2, XMax = 2;
        private const double YMin =   0, YMax = 8;

        public double X          { get; set; }
        public double Y          { get; set; }
        public double HeadingDeg { get; set; }
        public double SpeedMs    { get; set; }

        public TargetObject()
        {
            X          = -1;
            Y          = 3;
            HeadingDeg = 90;  // 0=위, +90=오른쪽, -90=왼쪽 (-180~180)
            SpeedMs    = 0.25;
        }

        // X 경계 충돌 시 heading *= -1 (좌우 반전). Y는 변하지 않으므로 클램프만.
        public void Advance(double deltaTimeSec)
        {
            double rad = HeadingDeg * Math.PI / 180.0;
            X += SpeedMs * Math.Sin(rad) * deltaTimeSec;
            Y += SpeedMs * Math.Cos(rad) * deltaTimeSec;

            if      (X < XMin) { X = XMin; HeadingDeg *= -1; }
            else if (X > XMax) { X = XMax; HeadingDeg *= -1; }

            Y = Math.Clamp(Y, YMin, YMax);
        }
    }
}
