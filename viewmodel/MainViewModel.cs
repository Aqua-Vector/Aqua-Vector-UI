using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AquaVectorUI.communication;
using AquaVectorUI.models;
using AquaVectorUI.services;

namespace AquaVectorUI.viewmodel
{
    // Core state: observable properties, collections, events, private helpers.
    public partial class MainViewModel : ObservableObject
    {
        // ── Infrastructure ───────────────────────────────────────────
        internal ICommunication? Communication;
        internal readonly ProtocolParser Parser = new();
        internal readonly Random Rng = new();
        internal DispatcherTimer? SimTimer;
        internal DispatcherTimer? PlaybackTimer;
        internal int PlaybackIndex;

        // ── Connection ───────────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UartVisibility))]
        [NotifyPropertyChangedFor(nameof(EthernetVisibility))]
        [NotifyPropertyChangedFor(nameof(IsEthernetSelected))]
        private bool _isUartSelected = true;

        [ObservableProperty] private string _portName = "COM21";
        [ObservableProperty] private int _baudRate = 115200;
        [ObservableProperty] private string _ipAddress = "192.168.1.100";
        [ObservableProperty] private int _tcpPort = 5000;
        [ObservableProperty] private string _inputText = "";
        [ObservableProperty] private bool _isConnected = false;
        [ObservableProperty] private string _connectionStatusText = "연결 안됨";

        // ── Torpedo / system state ────────────────────────────────────
        [ObservableProperty] private bool _isTorpedoOnline = false;
        [ObservableProperty] private bool _isDoorOpen = false;
        [ObservableProperty] private bool _isArmed = false;
        [ObservableProperty] private string _doorStatusText = "CLOSED";
        [ObservableProperty] private string _torpedoStatusText = "OFFLINE";

        // ── Torpedo world position (meters from origin) ───────────────
        [ObservableProperty] private double _torpedoWorldX = 0;
        [ObservableProperty] private double _torpedoWorldY = -80;
        [ObservableProperty] private double _torpedoHeadingDeg = 0;

        // ── Target ───────────────────────────────────────────────────
        [ObservableProperty] private double _targetWorldX = -40;
        [ObservableProperty] private double _targetWorldY = 60;
        [ObservableProperty] private bool _targetDetected = false;

        // ── Launch tube attitude ──────────────────────────────────────
        [ObservableProperty] private double _azimuthDeg = 0;
        [ObservableProperty] private double _elevationDeg = 0;

        // ── Selected map point ────────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AimAtTargetCommand))]
        [NotifyCanExecuteChangedFor(nameof(FireCommand))]
        private bool _hasSelectedPoint = false;

        [ObservableProperty] private double _selectedWorldX = 0;
        [ObservableProperty] private double _selectedWorldY = 0;
        [ObservableProperty] private string _selectedCoordText = "맵을 클릭하여 좌표 선택";
        [ObservableProperty] private string _hoverCoordText = "";

        // ── Path recording ────────────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopRecordingCommand))]
        [NotifyCanExecuteChangedFor(nameof(PlaybackPathCommand))]
        private bool _isRecordingPath = false;

        [ObservableProperty] private string _pathStatusText = "기록 없음";
        [ObservableProperty] private bool _isSimRunning = false;

        // ── Collections ───────────────────────────────────────────────
        public ObservableCollection<string> Logs { get; } = new();
        public ObservableCollection<PathPoint> PathPoints { get; } = new();
        public List<LidarPoint> CurrentLidarScan { get; } = new();

        // ── Render events (UserControls subscribe in Loaded) ──────────
        public event EventHandler? LidarUpdated;
        public event EventHandler? TorpedoPositionUpdated;
        public event EventHandler? TargetPositionUpdated;
        public event EventHandler? SelectedPointUpdated;

        // ── Computed properties ───────────────────────────────────────
        public Visibility UartVisibility => IsUartSelected ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EthernetVisibility => !IsUartSelected ? Visibility.Visible : Visibility.Collapsed;
        public bool IsEthernetSelected => !IsUartSelected;
        public int PathPointCount => PathPoints.Count;

        public MainViewModel()
        {
            PathPoints.CollectionChanged += (s, e) => OnPropertyChanged(nameof(PathPointCount));
            WireParserEvents();
        }

        private void WireParserEvents()
        {
            Parser.LidarUpdated += (s, e) =>
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentLidarScan.Clear();
                    CurrentLidarScan.AddRange(e.Points);
                    LidarUpdated?.Invoke(this, EventArgs.Empty);
                });

            Parser.PositionUpdated += (s, e) =>
                Application.Current.Dispatcher.Invoke(() =>
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

            Parser.DoorStatusChanged += (s, open) =>
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsDoorOpen = open;
                    DoorStatusText = open ? "OPEN" : "CLOSED";
                });

            Parser.TorpedoOnlineChanged += (s, online) =>
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsTorpedoOnline = online;
                    TorpedoStatusText = online ? "ONLINE" : "OFFLINE";
                });

            Parser.ArmedChanged += (s, armed) =>
                Application.Current.Dispatcher.Invoke(() => IsArmed = armed);

            Parser.TargetUpdated += (s, e) =>
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TargetWorldX = e.WorldX;
                    TargetWorldY = e.WorldY;
                    TargetDetected = e.Detected;
                    TargetPositionUpdated?.Invoke(this, EventArgs.Empty);
                });
        }

        // ── Private helpers (shared across partial files) ─────────────
        internal async System.Threading.Tasks.Task Transmit(string cmd)
        {
            if (Communication == null || !IsConnected) return;
            await Communication.SendAsync(cmd + "\n");
        }

        internal void OnDataReceived(string data) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog($"수신: {data}");
                Parser.Parse(data);
            });

        public void AppendLog(string message) =>
            Application.Current.Dispatcher.Invoke(() =>
                Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}"));

        internal static double ComputeAzimuth(double fromX, double fromY, double toX, double toY)
        {
            double dx = toX - fromX;
            double dy = toY - fromY;
            double deg = Math.Atan2(dx, dy) * 180.0 / Math.PI;
            return deg < 0 ? deg + 360.0 : deg;
        }
    }
}
