using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AquaVectorUI.viewmodel;

namespace AquaVectorUI.views
{
    public partial class GlobalMapControl : System.Windows.Controls.UserControl
    {
        private MainViewModel? _vm;

        // Canvas 500×500, origin (250,250), scale 2px/m
        private static double ToCanvasX(double wx) => 250.0 + wx * 2.0;
        private static double ToCanvasY(double wy) => 250.0 - wy * 2.0;

        public GlobalMapControl() => InitializeComponent();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm = (MainViewModel)DataContext;

            _vm.TorpedoPositionUpdated += (s, _) => RefreshTorpedo();
            _vm.TargetPositionUpdated  += (s, _) => RefreshTarget();
            _vm.SelectedPointUpdated   += (s, _) => RefreshSelectedMarker();
            _vm.PathPoints.CollectionChanged += (s, _) => RefreshPath();

            // Initial render
            RefreshTorpedo();
            RefreshTarget();
        }

        // ── Torpedo ──────────────────────────────────────────────────
        private void RefreshTorpedo()
        {
            if (_vm == null) return;

            double cx = Math.Clamp(ToCanvasX(_vm.TorpedoWorldX), 4, 496);
            double cy = Math.Clamp(ToCanvasY(_vm.TorpedoWorldY), 4, 496);
            double headRad = _vm.TorpedoHeadingDeg * Math.PI / 180.0;
            const double arrowLen = 22;
            double ax = cx + Math.Sin(headRad) * arrowLen;
            double ay = cy - Math.Cos(headRad) * arrowLen;

            System.Windows.Controls.Canvas.SetLeft(TorpedoCanvas, cx - 7);
            System.Windows.Controls.Canvas.SetTop(TorpedoCanvas, cy - 7);

            TorpedoHeadingLine.X1 = 7; TorpedoHeadingLine.Y1 = 7;
            TorpedoHeadingLine.X2 = 7 + (ax - cx); TorpedoHeadingLine.Y2 = 7 + (ay - cy);

            double perpX = -Math.Cos(headRad) * 4;
            double perpY = -Math.Sin(headRad) * 4;
            double localAx = ax - cx + 7;
            double localAy = ay - cy + 7;
            TorpedoArrow.Points = new PointCollection
            {
                new Point(localAx, localAy),
                new Point(localAx - Math.Sin(headRad) * 8 + perpX, localAy + Math.Cos(headRad) * 8 + perpY),
                new Point(localAx - Math.Sin(headRad) * 6,         localAy + Math.Cos(headRad) * 6),
                new Point(localAx - Math.Sin(headRad) * 8 - perpX, localAy + Math.Cos(headRad) * 8 - perpY),
            };

            System.Windows.Controls.Canvas.SetLeft(TorpedoLabel, 16);
            System.Windows.Controls.Canvas.SetTop(TorpedoLabel, 3);

            if (_vm.HasSelectedPoint)
                RefreshAimLine(cx, cy);
        }

        // ── Target ───────────────────────────────────────────────────
        private void RefreshTarget()
        {
            if (_vm == null) return;

            double cx = Math.Clamp(ToCanvasX(_vm.TargetWorldX), 4, 492);
            double cy = Math.Clamp(ToCanvasY(_vm.TargetWorldY), 4, 492);

            System.Windows.Controls.Canvas.SetLeft(TargetMarker, cx - 10);
            System.Windows.Controls.Canvas.SetTop(TargetMarker, cy - 9);
            System.Windows.Controls.Canvas.SetLeft(TargetLabel, cx - 6);
            System.Windows.Controls.Canvas.SetTop(TargetLabel, cy + 10);
        }

        // ── Selected point ───────────────────────────────────────────
        private void RefreshSelectedMarker()
        {
            if (_vm == null) return;

            if (!_vm.HasSelectedPoint)
            {
                SelectedCanvas.Visibility = Visibility.Hidden;
                AimLine.Opacity = 0;
                return;
            }

            double cx = Math.Clamp(ToCanvasX(_vm.SelectedWorldX), 4, 496);
            double cy = Math.Clamp(ToCanvasY(_vm.SelectedWorldY), 4, 496);

            System.Windows.Controls.Canvas.SetLeft(SelectedCanvas, cx);
            System.Windows.Controls.Canvas.SetTop(SelectedCanvas, cy);
            SelectedCanvas.Visibility = Visibility.Visible;

            double torpCx = Math.Clamp(ToCanvasX(_vm.TorpedoWorldX), 4, 496);
            double torpCy = Math.Clamp(ToCanvasY(_vm.TorpedoWorldY), 4, 496);
            RefreshAimLine(torpCx, torpCy);
        }

        private void RefreshAimLine(double torpCx, double torpCy)
        {
            if (_vm == null || !_vm.HasSelectedPoint) return;
            AimLine.X1 = torpCx; AimLine.Y1 = torpCy;
            AimLine.X2 = Math.Clamp(ToCanvasX(_vm.SelectedWorldX), 4, 496);
            AimLine.Y2 = Math.Clamp(ToCanvasY(_vm.SelectedWorldY), 4, 496);
            AimLine.Opacity = 0.7;
        }

        // ── Path polyline ────────────────────────────────────────────
        private void RefreshPath()
        {
            if (_vm == null) return;
            var pts = new PointCollection();
            foreach (var p in _vm.PathPoints)
                pts.Add(new Point(ToCanvasX(p.WorldX), ToCanvasY(p.WorldY)));
            PathPolyline.Points = pts;
        }

        // ── Mouse events ─────────────────────────────────────────────
        private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _vm?.HandleMapClick(e.GetPosition(MapCanvas).X, e.GetPosition(MapCanvas).Y);
        }

        private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            _vm?.HandleMapHover(e.GetPosition(MapCanvas).X, e.GetPosition(MapCanvas).Y);
        }
    }
}
