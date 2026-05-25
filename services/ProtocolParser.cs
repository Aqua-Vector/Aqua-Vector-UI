using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AquaVectorUI.models;

namespace AquaVectorUI.services
{
    public class LidarUpdateEventArgs : EventArgs
    {
        public List<LidarPoint> Points { get; init; } = new();
    }

    public class ObstacleUpdateEventArgs : EventArgs
    {
        public List<List<ObstaclePoint>> Obstacles { get; init; } = new();
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

    // �������� ���е� ���ڿ��� �Ľ��մϴ�.
    // ����
    // ���̴�:<angleDeg>,<distM>;<angleDeg>,<distM>;...
    // ��ǥ:<worldX>,<worldY>,<headingDeg>
    // ����:<OPEN|CLOSED>
    // ���:<ONLINE|OFFLINE>
    // ��ǥ:<worldX>,<worldY>,<0|1>
    public class ProtocolParser
    {
        public event EventHandler<LidarUpdateEventArgs>? LidarUpdated;
        public event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
        public event EventHandler<TargetUpdateEventArgs>? TargetUpdated;
        public event EventHandler<ObstacleUpdateEventArgs>? ObstacleUpdated;
        public event EventHandler<bool>? DoorStatusChanged;
        public event EventHandler<bool>? TorpedoOnlineChanged;
        public event EventHandler<bool>? ArmedChanged;
        public event EventHandler<string>? ParseError;

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
            else if (line.StartsWith("[["))
                ParseObstacles(line);
            else
                ParseError?.Invoke(this, $"알 수 없는 프리픽스: '{line}'");
        }

        private void ParseLidar(string data)
        {
            var points = new List<LidarPoint>();
            int badCount = 0;
            foreach (var pair in data.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(',');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double angle) &&
                    double.TryParse(parts[1], out double dist))
                {
                    points.Add(new LidarPoint { AngleDeg = angle, DistanceM = dist });
                }
                else
                {
                    badCount++;
                }
            }
            if (badCount > 0)
                ParseError?.Invoke(this, $"LIDAR: 파싱 실패 포인트 {badCount}개 (원본: '{data}')");
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
            else
            {
                ParseError?.Invoke(this, $"POS: 형식 오류 (필드 수={parts.Length}, 원본: '{data}')");
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
            else
            {
                ParseError?.Invoke(this, $"TARGET: 형식 오류 (필드 수={parts.Length}, 원본: '{data}')");
            }
        }

        // Obstacle group regex: matches [[x,y],[x,y],...] blocks
        private static readonly Regex _obstacleGroupRx =
            new(@"\[(\[-?\d+,-?\d+\](?:,\[-?\d+,-?\d+\])*)\]", RegexOptions.Compiled);
        private static readonly Regex _coordPairRx =
            new(@"\[(-?\d+),(-?\d+)\]", RegexOptions.Compiled);

        private void ParseObstacles(string data)
        {
            var obstacles = new List<List<ObstaclePoint>>();

            foreach (Match groupMatch in _obstacleGroupRx.Matches(data))
            {
                var points = new List<ObstaclePoint>();
                foreach (Match coord in _coordPairRx.Matches(groupMatch.Value))
                {
                    // Data format: [forwardDist, lateralOffset]
                    // lateral (2nd) → WorldX: negative = left, positive = right
                    // forward (1st) → WorldY: forward = up on display
                    // Each grid unit = 5 cm → convert to meters for rendering
                    points.Add(new ObstaclePoint
                    {
                        WorldX = int.Parse(coord.Groups[2].Value) * 0.05,
                        WorldY = int.Parse(coord.Groups[1].Value) * 0.05
                    });
                }
                if (points.Count > 0)
                    obstacles.Add(points);
            }

            if (obstacles.Count > 0)
                ObstacleUpdated?.Invoke(this, new ObstacleUpdateEventArgs { Obstacles = obstacles });
        }
    }
}
