using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Threading;
using AquaVectorUI.models;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        [RelayCommand]
        public void StartSimulation()
        {
            if (IsSimRunning) return;
            IsSimRunning = true;
            IsTorpedoOnline = true;
            TorpedoStatusText = "ONLINE (SIM)";

            double simAngle = 0;
            SimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            SimTimer.Tick += (s, e) =>
            {
                double rad = simAngle * Math.PI / 180.0;
                TorpedoWorldX = Math.Sin(rad) * 60;
                TorpedoWorldY = -80 + Math.Cos(rad) * 20;
                TorpedoHeadingDeg = (simAngle * 2) % 360;
                TorpedoPositionUpdated?.Invoke(this, EventArgs.Empty);

                if (IsRecordingPath)
                    PathPoints.Add(new PathPoint
                    {
                        Timestamp = DateTime.Now,
                        WorldX = TorpedoWorldX,
                        WorldY = TorpedoWorldY,
                        HeadingDeg = TorpedoHeadingDeg
                    });

                CurrentLidarScan.Clear();
                for (int i = 0; i <= 180; i += 3)
                {
                    double dist = 100 + Rng.NextDouble() * 80;
                    if (i >= 55 && i <= 70)   dist = 45 + Rng.NextDouble() * 8;
                    if (i >= 110 && i <= 125) dist = 60 + Rng.NextDouble() * 6;
                    CurrentLidarScan.Add(new LidarPoint
                    {
                        AngleDeg = i,
                        DistanceM = Math.Min(dist, 195)
                    });
                }
                LidarUpdated?.Invoke(this, EventArgs.Empty);

                simAngle = (simAngle + 3) % 360;
            };
            SimTimer.Start();
            AppendLog("시뮬레이션 시작됨 (SIM MODE)");
        }

        [RelayCommand]
        public void StopSimulation()
        {
            SimTimer?.Stop();
            SimTimer = null;
            IsSimRunning = false;
            IsTorpedoOnline = false;
            TorpedoStatusText = "OFFLINE";
            AppendLog("시뮬레이션 정지됨");
        }
    }
}
