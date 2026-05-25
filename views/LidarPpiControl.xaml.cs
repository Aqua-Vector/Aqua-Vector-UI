using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AquaVectorUI.viewmodel;

namespace AquaVectorUI.views
{
    public partial class LidarPpiControl : UserControl
    {
        // ── Scale (px/m) — updated on zoom ───────────────────────────
        private double _ppiScale = 200.0 / 7.0; // 1x: ~28.57px/m → max range 7m

        // ── PPI coordinate helpers ────────────────────────────────────
        // Origin (200,210), angle: 0°=left 90°=front 180°=right
        private double PpiX(double angleDeg, double distM) =>
            200.0 - distM * _ppiScale * Math.Cos(angleDeg * Math.PI / 180.0);
        private double PpiY(double angleDeg, double distM) =>
            210.0 - distM * _ppiScale * Math.Sin(angleDeg * Math.PI / 180.0);

        // Cartesian world → PPI canvas: right=+X, forward=+Y
        private double ObsCanvasX(double worldX) => 200.0 + worldX * _ppiScale;
        private double ObsCanvasY(double worldY) => 210.0 - worldY * _ppiScale;

        // ── Trail configuration ───────────────────────────────────────
        private const int    TrailLength = 28;
        private const int    FrameMs     = 6;

        // ── State ─────────────────────────────────────────────────────
        private MainViewModel?   _vm;
        private DispatcherTimer? _scanTimer;
        private double           _angleDeg  = 90.0;
        private int              _direction = 1;

        private readonly Line[]        _trailLines = new Line[TrailLength];
        private readonly Queue<double> _angleQueue = new();

        public LidarPpiControl() => InitializeComponent();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm = (MainViewModel)DataContext;
            _vm.LidarUpdated     += (_, _) => UpdateLidarDots();
            _vm.ObstaclesUpdated += (_, _) => UpdateObstacleDots();

            InitTrailLines();

            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameMs) };
            _scanTimer.Tick += OnScanTick;
            _scanTimer.Start();

            SetPpiZoom(1.0); // initialize button states and labels
        }

        private void InitTrailLines()
        {
            for (int i = 0; i < TrailLength; i++)
            {
                var line = new Line
                {
                    X1 = 200, Y1 = 210, X2 = 200, Y2 = 10,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xdd, 0x55)),
                    StrokeThickness = 1.4,
                    Opacity = 0,
                    IsHitTestVisible = false
                };
                _trailLines[i] = line;
                TrailCanvas.Children.Add(line);
            }
        }

        // ── Scan tick (sweep animation currently disabled) ────────────
        private void OnScanTick(object? sender, EventArgs e) { }

        // ── Zoom ──────────────────────────────────────────────────────
        private void PpiZoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), out double factor))
                SetPpiZoom(factor);
        }

        private void SetPpiZoom(double factor)
        {
            _ppiScale = (200.0 / 7.0) * factor;

            // Ring labels: ring pixel radii are 40,80,120,160,200 → distances = radius/_ppiScale
            RingLabel1.Text = FormatM(40.0  / _ppiScale);
            RingLabel2.Text = FormatM(80.0  / _ppiScale);
            RingLabel3.Text = FormatM(120.0 / _ppiScale);
            RingLabel4.Text = FormatM(160.0 / _ppiScale);
            RingLabel5.Text = FormatM(200.0 / _ppiScale);

            // Highlight active button
            foreach (var (btn, tag) in new[] {
                (PpiZoom1x, 1.0), (PpiZoom2x, 2.0), (PpiZoom4x, 4.0) })
            {
                bool active = tag == factor;
                btn.Background   = new SolidColorBrush(active ? Color.FromRgb(0x1d, 0x4a, 0x20) : Color.FromRgb(0x08, 0x10, 0x08));
                btn.Foreground   = new SolidColorBrush(active ? Color.FromRgb(0x88, 0xff, 0x88) : Color.FromRgb(0x33, 0x66, 0x33));
                btn.BorderBrush  = new SolidColorBrush(active ? Color.FromRgb(0x44, 0xaa, 0x44) : Color.FromRgb(0x1a, 0x3a, 0x1a));
            }

            UpdateLidarDots();
            UpdateObstacleDots();
        }

        private static string FormatM(double meters)
        {
            double rounded = Math.Round(meters, 3);
            return (rounded == Math.Floor(rounded))
                ? $"{(int)rounded}m"
                : $"{rounded:0.##}m";
        }

        // ── Obstacle dot rendering ────────────────────────────────────
        private void UpdateObstacleDots()
        {
            if (_vm == null) return;
            ObstacleDotCanvas.Children.Clear();

            foreach (var obstacle in _vm.CurrentObstacles)
            {
                foreach (var pt in obstacle)
                {
                    double cx = ObsCanvasX(pt.WorldX);
                    double cy = ObsCanvasY(pt.WorldY);
                    if (cx < 0 || cx > 400 || cy < 0 || cy > 210) continue;

                    var dot = new Ellipse
                    {
                        Width = 5, Height = 5,
                        Fill = new SolidColorBrush(Color.FromRgb(0xff, 0xaa, 0x00)),
                        Opacity = 0.9
                    };
                    Canvas.SetLeft(dot, cx - 2.5);
                    Canvas.SetTop(dot, cy - 2.5);
                    ObstacleDotCanvas.Children.Add(dot);
                }
            }
        }

        // ── LiDAR dot rendering ───────────────────────────────────────
        private void UpdateLidarDots()
        {
            if (_vm == null) return;
            LidarDotCanvas.Children.Clear();

            double maxDist = 200.0 / _ppiScale; // = 10m at 1x, 5m at 2x, 2.5m at 4x

            foreach (var pt in _vm.CurrentLidarScan)
            {
                if (pt.DistanceM <= 0 || pt.DistanceM > maxDist) continue;

                double cx = PpiX(pt.AngleDeg, pt.DistanceM);
                double cy = PpiY(pt.AngleDeg, pt.DistanceM);
                if (cx < 0 || cx > 400 || cy < 0 || cy > 210) continue;

                bool isClose = pt.DistanceM < maxDist * 0.3;
                var dot = new Ellipse
                {
                    Width  = isClose ? 5 : 3,
                    Height = isClose ? 5 : 3,
                    Fill   = isClose
                        ? new SolidColorBrush(Color.FromRgb(0xff, 0x88, 0x00))
                        : new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0x44)),
                    Opacity = isClose ? 1.0 : 0.85
                };
                Canvas.SetLeft(dot, cx - dot.Width  / 2);
                Canvas.SetTop (dot, cy - dot.Height / 2);
                LidarDotCanvas.Children.Add(dot);
            }
        }
    }
}
