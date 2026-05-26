using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AquaVectorUI.communication;
using AquaVectorUI.models;
using AquaVectorUI.services;
using System.Linq;

namespace AquaVectorUI.viewmodel
{
    // Core state: observable properties, collections, events, private helpers.
    public partial class MainViewModel : ObservableObject
    {
        // ── Infrastructure ───────────────────────────────────────────
        internal ICommunication? Communication;
        internal readonly ProtocolParser Parser = new();
        internal readonly LidarPacketParser LidarBinaryParser = new();
        internal readonly TargetPacketParser TargetBinaryParser = new();
        internal UdpLidarReceiver? UdpLidarReceiver;
        internal readonly Random Rng = new();
        private ushort _cmdSeq = 0;

        // ── Torpedo heartbeat (UDP coordinate timeout → OFFLINE) ──────
        private readonly DispatcherTimer _torpedoHeartbeatTimer;
        private static readonly TimeSpan TorpedoTimeout = TimeSpan.FromSeconds(3);

        // ── Connection ───────────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UartVisibility))]
        [NotifyPropertyChangedFor(nameof(EthernetVisibility))]
        [NotifyPropertyChangedFor(nameof(IsEthernetSelected))]
        private bool _isUartSelected = true;

        [ObservableProperty] private string _portName = "COM21";
        [ObservableProperty] private int _baudRate = 115200;
        [ObservableProperty] private string _ipAddress = "192.168.1.101";
        [ObservableProperty] private int _tcpPort = 5000;
        [ObservableProperty] private int _udpPort = 4000;
        [ObservableProperty] private string _inputText = "";
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(FireCommand))]
        private bool _isConnected = false;
        [ObservableProperty] private string _connectionStatusText = "연결 안됨";

        // ── Torpedo / system state ────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(FireCommand))]
        private bool _isTorpedoOnline = false;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(FireCommand))]
        private bool _isDoorOpen = false;
        [ObservableProperty] private bool _isArmed = false;
        [ObservableProperty] private string _doorStatusText = "CLOSED";
        [ObservableProperty] private string _torpedoStatusText = "OFFLINE";

        // ── Torpedo world position (meters from origin) ───────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DistanceToTarget))]
        private double _torpedoWorldX = 0;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DistanceToTarget))]
        private double _torpedoWorldY = 0;
        [ObservableProperty] private double _torpedoHeadingDeg = 0;

        // ── Target ───────────────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DistanceToTarget))]
        private double _targetWorldX = 0;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DistanceToTarget))]
        private double _targetWorldY = 0;
        [ObservableProperty] private double _targetHeadingDeg = 0;
        [ObservableProperty] private double _targetSpeedMs = 0;
        [ObservableProperty] private bool _targetDetected = false;

        // ── Launch tube attitude ──────────────────────────────────────
        [ObservableProperty] private double _azimuthDeg = 0;
        [ObservableProperty] private double _elevationDeg = 0;

        // ── Selected map point ────────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AimAtTargetCommand))]
        private bool _hasSelectedPoint = false;

        [ObservableProperty] private double _selectedWorldX = 0;
        [ObservableProperty] private double _selectedWorldY = 0;
        [ObservableProperty] private string _selectedCoordText = "맵을 클릭하여 좌표 선택";
        [ObservableProperty] private string _hoverCoordText = "";

        // ── Path recording ────────────────────────────────────────────
        [ObservableProperty] private bool _isRecordingPath = false;
        [ObservableProperty] private string _pathStatusText = "기록 없음";

        // ── Torpedo object (canonical live state) ────────────────────
        public Torpedo CurrentTorpedo { get; } = new();

        // ── Collections ───────────────────────────────────────────────
        public ObservableCollection<string> Logs { get; } = new();
        public ObservableCollection<PathPoint> PathPoints { get; } = new();
        public List<LidarPoint> CurrentLidarScan { get; } = new();
        public List<List<ObstaclePoint>> CurrentObstacles { get; } = new();

        // ── Render events (UserControls subscribe in Loaded) ──────────
        public event EventHandler? LidarUpdated;
        public event EventHandler? ObstaclesUpdated;
        public event EventHandler? TorpedoPositionUpdated;
        public event EventHandler? TargetPositionUpdated;
        public event EventHandler? SelectedPointUpdated;

        // ── Computed properties ───────────────────────────────────────
        public Visibility UartVisibility => IsUartSelected ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EthernetVisibility => !IsUartSelected ? Visibility.Visible : Visibility.Collapsed;
        public bool IsEthernetSelected => !IsUartSelected;
        public int PathPointCount => PathPoints.Count;
        public double DistanceToTarget => Math.Round(
            Math.Sqrt(Math.Pow(TorpedoWorldX - TargetWorldX, 2) +
                      Math.Pow(TorpedoWorldY - TargetWorldY, 2)), 2);

        public MainViewModel()
        {
            _torpedoHeartbeatTimer = new DispatcherTimer { Interval = TorpedoTimeout };
            _torpedoHeartbeatTimer.Tick += (s, e) =>
            {
                _torpedoHeartbeatTimer.Stop();
                IsTorpedoOnline = false;
                TorpedoStatusText = "OFFLINE";
            };

            PathPoints.CollectionChanged += (s, e) => OnPropertyChanged(nameof(PathPointCount));
            WireParserEvents();
            WireLidarBinaryEvents();
            WireImuBinaryEvents();
            StartUdpReceiver();
            StartTargetTracking();
        }

        private void ResetTorpedoHeartbeat()
        {
            _torpedoHeartbeatTimer.Stop();
            _torpedoHeartbeatTimer.Start();
            if (!IsTorpedoOnline)
            {
                IsTorpedoOnline = true;
                TorpedoStatusText = "ONLINE";
            }
        }

        private void WireParserEvents()
        {
            Parser.LidarUpdated += (s, e) =>
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentLidarScan.Clear();
                    CurrentLidarScan.AddRange(e.Points);
                    LidarUpdated?.Invoke(this, EventArgs.Empty);
                });

            Parser.PositionUpdated += (s, e) =>
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TorpedoWorldX = e.WorldX;
                    TorpedoWorldY = e.WorldY;
                    TorpedoHeadingDeg = e.HeadingDeg;
                    TorpedoPositionUpdated?.Invoke(this, EventArgs.Empty);
                    if (IsRecordingPath)
                        PathPoints.Add(new PathPoint
                        {
                            Timestamp = DateTime.Now,
                            WorldX = e.WorldX,
                            WorldY = e.WorldY,
                            HeadingDeg = e.HeadingDeg
                        });
                });

            Parser.ObstacleUpdated += (s, e) =>
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentObstacles.Clear();
                    CurrentObstacles.AddRange(e.Obstacles);
                    ObstaclesUpdated?.Invoke(this, EventArgs.Empty);
                });

            Parser.DoorStatusChanged += (s, open) =>
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsDoorOpen = open;
                    DoorStatusText = open ? "OPEN" : "CLOSED";
                });

            Parser.TorpedoOnlineChanged += (s, online) =>
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsTorpedoOnline = online;
                    TorpedoStatusText = online ? "ONLINE" : "OFFLINE";
                });

            Parser.ArmedChanged += (s, armed) =>
                Application.Current.Dispatcher.InvokeAsync(() => IsArmed = armed);

            Parser.TargetUpdated += (s, e) =>
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TargetWorldX = e.WorldX;
                    TargetWorldY = e.WorldY;
                    TargetDetected = e.Detected;
                    TargetPositionUpdated?.Invoke(this, EventArgs.Empty);
                });

            Parser.ParseError += (s, msg) =>
                Application.Current.Dispatcher.InvokeAsync(() => AppendLog($"[파싱 오류] {msg}"));
        }

        private void WireLidarBinaryEvents()
        {
            LidarBinaryParser.PacketParsed += (s, e) =>
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentObstacles.Clear();
                    CurrentObstacles.AddRange(
                        e.Packet.Objects.Select(o => o.Points));
                    ObstaclesUpdated?.Invoke(this, EventArgs.Empty);

                    var t = e.Packet.Torpedo;
                    CurrentTorpedo.X   = t.X;
                    CurrentTorpedo.Y   = t.Y;
                    CurrentTorpedo.Yaw = t.Yaw;
                    TorpedoWorldX      = t.X;
                    TorpedoWorldY      = t.Y;
                    TorpedoHeadingDeg  = t.Yaw;
                    TorpedoPositionUpdated?.Invoke(this, EventArgs.Empty);
                    ResetTorpedoHeartbeat();

                    // AppendLog($"[UDP] LiDAR seq={e.Packet.Seq}, 어뢰=({t.X:F2},{t.Y:F2}) yaw={t.Yaw:F1}°, 장애물 수={e.Packet.Objects.Count}");
                });

            LidarBinaryParser.ParseError += (s, msg) =>
                Application.Current.Dispatcher.InvokeAsync(() => AppendLog($"[UDP] {msg}"));
        }

        private void WireImuBinaryEvents()
        {
            TargetBinaryParser.PacketParsed += (s, e) =>
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentTorpedo.X = e.Packet.X;
                    CurrentTorpedo.Y = e.Packet.Y;
                    TorpedoWorldX    = e.Packet.X;
                    TorpedoWorldY    = e.Packet.Y;
                    TorpedoPositionUpdated?.Invoke(this, EventArgs.Empty);
                    ResetTorpedoHeartbeat();
                    AppendLog($"[UDP] IMU seq={e.Packet.Seq}  위치: ({e.Packet.X:F2}, {e.Packet.Y:F2})");
                });

            TargetBinaryParser.ParseError += (s, msg) =>
                Application.Current.Dispatcher.InvokeAsync(() => AppendLog($"[UDP] {msg}"));
        }

        // Routes incoming UDP packets by size: TorpedoPacket is exactly 14 B (fixed);
        // LiDAR packets are ≥ 15 B (13 B header + ≥ 0 objects + 2 B CRC).
        private void DispatchUdpPacket(byte[] data)
        {
            if (data.Length < 7) return;
            if (data.Length == 14)
                TargetBinaryParser.Parse(data);
            else
                LidarBinaryParser.Parse(data);
        }

        private void StartUdpReceiver()
        {
            try
            {
                UdpLidarReceiver = new UdpLidarReceiver(UdpPort);
                UdpLidarReceiver.PacketReceived += DispatchUdpPacket;
                UdpLidarReceiver.Error += msg => AppendLog($"[UDP] {msg}");
                string? localIp = UdpLidarReceiver.Start();
                string ipInfo = localIp != null ? $" / 내 IP: {localIp}" : "";
                AppendLog($"[UDP] 수신 대기 중 (포트 {UdpPort}{ipInfo})");
            }
            catch (Exception ex)
            {
                AppendLog($"[UDP] 포트 {UdpPort} 개방 실패: {ex.Message}");
            }
        }

        // ── Private helpers (shared across partial files) ─────────────
        internal async System.Threading.Tasks.Task Transmit(string cmd)
        {
            if (Communication == null || !IsConnected) return;
            await Communication.SendAsync(cmd + "\n");
        }

        internal async System.Threading.Tasks.Task TransmitBytes(byte[] data)
        {
            if (Communication == null || !IsConnected) return;
            await Communication.SendBytesAsync(data);
            AppendLog($"[TX] {string.Join(" ", data.Select(b => $"{b:X2}"))}");
        }
        
        internal ushort NextCmdSeq() => _cmdSeq++;

        internal void OnDataReceived(string data) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog($"수신: {data}");
                Parser.Parse(data);
            });

        private const int MaxLogEntries = 500;

        public void AppendLog(string message) =>
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (Logs.Count >= MaxLogEntries)
                    Logs.RemoveAt(0);
                Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            });

        internal static double ComputeAzimuth(double fromX, double fromY, double toX, double toY)
        {
            double dx = toX - fromX;
            double dy = toY - fromY;
            double deg = Math.Atan2(dx, dy) * 180.0 / Math.PI;
            return deg < 0 ? deg + 360.0 : deg;
        }
    }
}
