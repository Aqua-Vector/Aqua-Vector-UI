using System;
using System.Collections.Generic;
using AquaVectorUI.models;

namespace AquaVectorUI.services
{
    public class LidarUpdateEventArgs : EventArgs
    {
        public List<LidarPoint> Points { get; init; } = new();
    }

    public class PositionUpdateEventArgs : EventArgs
    {
        public double WorldX { get; init; }
        public double WorldY { get; init; }
        public double HeadingDeg { get; init; }
    }

    public class TargetUpdateEventArgs : EventArgs
    {
        public double WorldX { get; init; }
        public double WorldY { get; init; }
        public bool Detected { get; init; }
    }

    // 개행으로 구분된 문자열을 파싱합니다.
    // 예시
    // 라이다:<angleDeg>,<distM>;<angleDeg>,<distM>;...
    // 좌표:<worldX>,<worldY>,<headingDeg>
    // 개폐:<OPEN|CLOSED>
    // 어뢰:<ONLINE|OFFLINE>
    // 목표:<worldX>,<worldY>,<0|1>
    public class ProtocolParser
    {
        public event EventHandler<LidarUpdateEventArgs>? LidarUpdated;
        public event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
        public event EventHandler<TargetUpdateEventArgs>? TargetUpdated;
        public event EventHandler<bool>? DoorStatusChanged;
        public event EventHandler<bool>? TorpedoOnlineChanged;
        public event EventHandler<bool>? ArmedChanged;

        public void Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            line = line.Trim();

            if (line.StartsWith("LIDAR:"))
                ParseLidar(line[6..]);
            else if (line.StartsWith("POS:"))
                ParsePosition(line[4..]);
            else if (line.StartsWith("DOOR:"))
                DoorStatusChanged?.Invoke(this, line[5..].Equals("OPEN", StringComparison.OrdinalIgnoreCase));
            else if (line.StartsWith("TORPEDO:"))
                TorpedoOnlineChanged?.Invoke(this, line[8..].Equals("ONLINE", StringComparison.OrdinalIgnoreCase));
            else if (line.StartsWith("ARMED:"))
                ArmedChanged?.Invoke(this, line[6..].Equals("TRUE", StringComparison.OrdinalIgnoreCase));
            else if (line.StartsWith("TARGET:"))
                ParseTarget(line[7..]);
        }

        private void ParseLidar(string data)
        {
            var points = new List<LidarPoint>();
            foreach (var pair in data.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(',');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double angle) &&
                    double.TryParse(parts[1], out double dist))
                {
                    points.Add(new LidarPoint { AngleDeg = angle, DistanceM = dist });
                }
            }
            if (points.Count > 0)
                LidarUpdated?.Invoke(this, new LidarUpdateEventArgs { Points = points });
        }

        private void ParsePosition(string data)
        {
            var parts = data.Split(',');
            if (parts.Length >= 3 &&
                double.TryParse(parts[0], out double x) &&
                double.TryParse(parts[1], out double y) &&
                double.TryParse(parts[2], out double h))
            {
                PositionUpdated?.Invoke(this, new PositionUpdateEventArgs { WorldX = x, WorldY = y, HeadingDeg = h });
            }
        }

        private void ParseTarget(string data)
        {
            var parts = data.Split(',');
            if (parts.Length >= 3 &&
                double.TryParse(parts[0], out double x) &&
                double.TryParse(parts[1], out double y) &&
                int.TryParse(parts[2], out int det))
            {
                TargetUpdated?.Invoke(this, new TargetUpdateEventArgs { WorldX = x, WorldY = y, Detected = det != 0 });
            }
        }
    }
}
