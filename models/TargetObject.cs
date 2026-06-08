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

        private readonly double _direction; // +1 or -1

        public TargetObject()
        {
            StopX      = EnvConfig.GetDouble("TARGET_STOP_X",       -1.8);
            X          = EnvConfig.GetDouble("TARGET_START_X",       1.8);
            Y          = EnvConfig.GetDouble("TARGET_START_Y",       3.0);
            HeadingDeg = EnvConfig.GetDouble("TARGET_HEADING_DEG",  90.0);
            SpeedMs    = EnvConfig.GetDouble("TARGET_SPEED_MS",      0.10);
            _direction = StopX >= X ? 1.0 : -1.0;
        }

        // X축으로만 이동, StopX 도달 시 정지. 방향은 시작/종점으로 자동 결정.
        public void Advance(double deltaTimeSec)
        {
            if (_direction > 0 ? X >= StopX : X <= StopX) return;

            X += _direction * SpeedMs * deltaTimeSec;
            if (_direction > 0 ? X > StopX : X < StopX) X = StopX;
        }
    }
}
