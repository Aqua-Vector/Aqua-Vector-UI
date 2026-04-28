using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AquaVectorUI.viewmodel;

namespace AquaVectorUI.views
{
    public partial class LidarPpiControl : System.Windows.Controls.UserControl
    {
        // ── PPI coordinate helpers ────────────────────────────────────
        // Origin (200,210), 1px = 1m, angle: 0°=left 90°=front 180°=right
        private static double PpiX(double angleDeg, double distM) =>
            200.0 - distM * Math.Cos(angleDeg * Math.PI / 180.0);
        private static double PpiY(double angleDeg, double distM) =>
            210.0 - distM * Math.Sin(angleDeg * Math.PI / 180.0);

        // ── Trail configuration ───────────────────────────────────────
        private const int    TrailLength  = 28;    // number of ghost lines
        private const double SweepStep    = 2.6;   // degrees per tick
        private const int    FrameMs      = 6;    // ~36 fps

        // ── State ─────────────────────────────────────────────────────
        private MainViewModel?   _vm;
        private DispatcherTimer? _scanTimer;
        private double           _angleDeg  = 90.0;
        private int              _direction = 1;

        // Pre-allocated trail lines: avoids per-frame GC allocation
        private readonly Line[]          _trailLines = new Line[TrailLength];
        private readonly Queue<double>   _angleQueue = new();

        public LidarPpiControl() => InitializeComponent();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm = (MainViewModel)DataContext;
            _vm.LidarUpdated += (_, _) => UpdateLidarDots();

            InitTrailLines();

            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameMs) };
            _scanTimer.Tick += OnScanTick;
            _scanTimer.Start();
        }

        // Pre-create trail line objects and add them to TrailCanvas once.
        private void InitTrailLines()
        {
            for (int i = 0; i < TrailLength; i++)
            {
                var line = new Line
                {
                    X1 = 200, Y1 = 210,
                    X2 = 200, Y2 = 14,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xdd, 0x55)),
                    StrokeThickness = 1.4,
                    Opacity = 0,
                    IsHitTestVisible = false
                };
                _trailLines[i] = line;
                TrailCanvas.Children.Add(line);
            }
        }

        // ── Scan tick ─────────────────────────────────────────────────
        private void OnScanTick(object? sender, EventArgs e)
        {
            // Advance angle
            //_angleDeg += _direction * SweepStep;
            //if (_angleDeg >= 180.0) { _angleDeg = 0.0; _direction = 1; }
            //else if (_angleDeg <= 0.0) { _angleDeg = 0.0; _direction = 1; }

            //// Push to history queue
            //_angleQueue.Enqueue(_angleDeg);
            //if (_angleQueue.Count > TrailLength)
            //    _angleQueue.Dequeue();

            //// Update scan line + glow to current position
            //double tipX = PpiX(_angleDeg, 196);
            //double tipY = PpiY(_angleDeg, 196);
            //ScanLine.X2  = tipX;
            //ScanLine.Y2  = tipY;
            //GlowLine.X2  = tipX;
            //GlowLine.Y2  = tipY;

            //// Update trail lines with quadratic opacity falloff
            //// angles[0] = oldest, angles[n-1] = newest (just behind scan line)
            //double[] angles = [.. _angleQueue];
            //int count = angles.Length;

            //for (int i = 0; i < TrailLength; i++)
            //{
            //    if (i < count)
            //    {
            //        // Normalised position: 0 = oldest, 1 = newest
            //        double t = (i + 1.0) / count;

            //        // Quadratic falloff → sharp fade at tail, bright near scan line
            //        double opacity = Math.Pow(t, 2.2) * 0.55;

            //        _trailLines[i].X2 = PpiX(angles[i], 195);
            //        _trailLines[i].Y2 = PpiY(angles[i], 195);
            //        _trailLines[i].Opacity = opacity;
            //    }
            //    else
            //    {
            //        _trailLines[i].Opacity = 0;
            //    }
            //}
        }

        // ── LiDAR dot rendering ───────────────────────────────────────
        private void UpdateLidarDots()
        {
            if (_vm == null) return;
            LidarDotCanvas.Children.Clear();

            foreach (var pt in _vm.CurrentLidarScan)
            {
                if (pt.DistanceM <= 0 || pt.DistanceM > 198) continue;

                double cx = PpiX(pt.AngleDeg, pt.DistanceM);
                double cy = PpiY(pt.AngleDeg, pt.DistanceM);
                if (cx < 0 || cx > 400 || cy < 0 || cy > 210) continue;

                bool isClose = pt.DistanceM < 60;
                var dot = new Ellipse
                {
                    Width  = isClose ? 5 : 3,
                    Height = isClose ? 5 : 3,
                    Fill   = isClose
                        ? new SolidColorBrush(Color.FromRgb(0xff, 0x88, 0x00))
                        : new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0x44)),
                    Opacity = isClose ? 1.0 : 0.85
                };
                System.Windows.Controls.Canvas.SetLeft(dot, cx - dot.Width  / 2);
                System.Windows.Controls.Canvas.SetTop (dot, cy - dot.Height / 2);
                LidarDotCanvas.Children.Add(dot);
            }
        }
    }
}
