using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AquaVectorUI.viewmodel;

namespace AquaVectorUI.views
{
    public partial class GlobalMapControl : UserControl
    {
        private MainViewModel? _vm;

        // Canvas 460×460. Origin = canvas position of world (0,0).
        // X: -2~2m (4m) → 420px  scaleX = 105.0 px/m  (= _mapScale * XYRatio)
        // Y:  0~8m (8m) → 420px  scaleY =  52.5 px/m  (= _mapScale)
        // → 0.5m X cell = 52.5px = 1m Y cell : perfect square grid
        private double _mapScale = 52.5;  // Y px/m; X scale = _mapScale * XYRatio
        private double _originX  = 230.0; // canvas X for world X=0
        private double _originY  = 440.0; // canvas Y for world Y=0

        // _mapScale is the Y scale. Cannot zoom out past DefaultScale.
        private const double DefaultScale = 52.5;
        private const double MaxScale     = 200.0;
        private const double XYRatio      = 2.0; // scaleX / scaleY = 105 / 52.5
        private const double CanvasLeft   = 20;
        private const double CanvasRight  = 440;
        private const double CanvasTop    = 20;
        private const double CanvasBottom = 440;

        private enum InfoTarget { None, Torpedo, Target, Obstacle }
        private InfoTarget _infoTarget = InfoTarget.None;
        private double _infoObstacleWorldX, _infoObstacleWorldY;

        private double ToCanvasX(double wx) => _originX + wx * _mapScale * XYRatio;
        private double ToCanvasY(double wy) => _originY - wy * _mapScale;
        private double ToWorldX(double cx)  => (cx - _originX) / (_mapScale * XYRatio);
        private double ToWorldY(double cy)  => (_originY - cy) / _mapScale;

        public GlobalMapControl() => InitializeComponent();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm = (MainViewModel)DataContext;
            _vm.TorpedoPositionUpdated          += (_, _) => RefreshTorpedo();
            _vm.TargetPositionUpdated           += (_, _) => RefreshTarget();
            _vm.SelectedPointUpdated            += (_, _) => RefreshSelectedMarker();
            _vm.ObstaclesUpdated                += (_, _) => RefreshObstacles();
            _vm.PathPoints.CollectionChanged    += (_, _) => RefreshPath();
            RefreshAll();
        }

        // ── Reset ─────────────────────────────────────────────────────
        private void MapReset_Click(object sender, RoutedEventArgs e)
        {
            ResetToDefault();
            RefreshAll();
        }

        // ── Scroll zoom to cursor ─────────────────────────────────────
        private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            InfoOverlay.Visibility = Visibility.Collapsed;

            // Zoom out: if already at minimum, snap to exact default and stop
            if (e.Delta < 0 && _mapScale <= DefaultScale)
            {
                ResetToDefault();
                RefreshAll();
                e.Handled = true;
                return;
            }

            var    pos = e.GetPosition(MapCanvas);
            double wx  = ToWorldX(pos.X);
            double wy  = ToWorldY(pos.Y);

            double newScale = Math.Clamp(_mapScale * (e.Delta > 0 ? 1.15 : 1.0 / 1.15),
                                         DefaultScale, MaxScale);

            if (newScale <= DefaultScale)
            {
                // Zooming out hit the floor — snap to clean default view
                ResetToDefault();
            }
            else
            {
                _mapScale = newScale;
                _originX  = pos.X - wx * _mapScale * XYRatio;
                _originY  = pos.Y + wy * _mapScale;
            }

            RefreshAll();
            e.Handled = true;
        }

        private void ResetToDefault()
        {
            _mapScale = DefaultScale;
            _originX  = 230.0;
            _originY  = 440.0;
        }

        // ── Full refresh ──────────────────────────────────────────────
        private void RefreshAll()
        {
            RedrawGrid();
            RedrawAxisLabels();
            UpdateOriginDot();
            UpdateTitle();
            RefreshTorpedo();
            RefreshTarget();
            RefreshSelectedMarker();
            RefreshPath();
            RefreshObstacles();
        }

        // ── Grid ──────────────────────────────────────────────────────
        private void RedrawGrid()
        {
            GridCanvas.Children.Clear();

            double intervalX = GetGridIntervalX();
            double intervalY = GetGridIntervalY();
            double wLeft     = ToWorldX(CanvasLeft);
            double wRight    = ToWorldX(CanvasRight);
            double wTop      = ToWorldY(CanvasTop);
            double wBottom   = ToWorldY(CanvasBottom);

            // Vertical lines (X axis) — 0.5m intervals at default zoom
            double startX = Math.Ceiling(wLeft / intervalX) * intervalX;
            for (double wx = startX; wx <= wRight + intervalX * 0.001; wx += intervalX)
            {
                double cx = ToCanvasX(wx);
                if (cx < CanvasLeft - 0.5 || cx > CanvasRight + 0.5) continue;

                bool isCenter = Math.Abs(wx)       < 0.001;
                bool isMajor  = !isCenter && Math.Abs(wx % (intervalY)) < 0.001; // whole-meter marks

                GridCanvas.Children.Add(new Line
                {
                    X1 = cx, Y1 = CanvasTop, X2 = cx, Y2 = CanvasBottom,
                    Stroke = new SolidColorBrush(
                        isCenter ? Color.FromRgb(0x22, 0x77, 0x22) :
                        isMajor  ? Color.FromRgb(0x1a, 0x5a, 0x1a) :
                                   Color.FromRgb(0x14, 0x42, 0x14)),
                    StrokeThickness = isCenter ? 1.2 : isMajor ? 0.9 : 0.7,
                    Opacity = isCenter ? 1.0 : isMajor ? 0.95 : 0.9
                });
            }

            // Horizontal lines (Y axis, world Y ≥ 0 only) — 1m intervals at default zoom
            double startY = Math.Floor(wTop / intervalY) * intervalY;
            for (double wy = startY; wy >= wBottom - intervalY * 0.001; wy -= intervalY)
            {
                if (wy < 0) continue;
                double cy = ToCanvasY(wy);
                if (cy < CanvasTop - 0.5 || cy > CanvasBottom + 0.5) continue;

                bool isBaseline = Math.Abs(wy)          < 0.001;
                bool isMajor    = !isBaseline && Math.Abs(wy % (intervalY * 2)) < 0.001;

                GridCanvas.Children.Add(new Line
                {
                    X1 = CanvasLeft, Y1 = cy, X2 = CanvasRight, Y2 = cy,
                    Stroke = new SolidColorBrush(
                        isBaseline ? Color.FromRgb(0x22, 0x77, 0x22) :
                        isMajor    ? Color.FromRgb(0x1a, 0x5a, 0x1a) :
                                     Color.FromRgb(0x14, 0x42, 0x14)),
                    StrokeThickness = isBaseline ? 1.4 : isMajor ? 0.9 : 0.7,
                    Opacity = isBaseline ? 1.0 : isMajor ? 0.95 : 0.9
                });
            }

            // Valid-area border
            var borderBrush = new SolidColorBrush(Color.FromRgb(0x1a, 0x4a, 0x1a));
            GridCanvas.Children.Add(new Line { X1=CanvasLeft,  Y1=CanvasTop,    X2=CanvasRight, Y2=CanvasTop,    Stroke=borderBrush, StrokeThickness=1.0 });
            GridCanvas.Children.Add(new Line { X1=CanvasLeft,  Y1=CanvasBottom, X2=CanvasRight, Y2=CanvasBottom, Stroke=borderBrush, StrokeThickness=1.2 });
            GridCanvas.Children.Add(new Line { X1=CanvasLeft,  Y1=CanvasTop,    X2=CanvasLeft,  Y2=CanvasBottom, Stroke=borderBrush, StrokeThickness=1.0 });
            GridCanvas.Children.Add(new Line { X1=CanvasRight, Y1=CanvasTop,    X2=CanvasRight, Y2=CanvasBottom, Stroke=borderBrush, StrokeThickness=1.0 });
        }

        // X grid at half the Y interval — keeps cells visually near-square (scaleX:scaleY = 7:4 ≈ 2:1)
        private double GetGridIntervalY() => _mapScale >= 12 ? 1.0 : _mapScale >= 6 ? 2.0 : 5.0;
        private double GetGridIntervalX() => GetGridIntervalY() * 0.5;

        // ── Axis labels ───────────────────────────────────────────────
        private void RedrawAxisLabels()
        {
            AxisLabelCanvas.Children.Clear();
            var brush = new SolidColorBrush(Color.FromRgb(0x55, 0xbb, 0x55));
            var font  = new FontFamily("Consolas");

            double labelIntervalY = GetLabelIntervalY();
            double labelIntervalX = GetLabelIntervalX();

            // Y labels (left side)
            double wTop    = ToWorldY(CanvasTop);
            double wBottom = ToWorldY(CanvasBottom);
            double startY  = Math.Floor(wTop / labelIntervalY) * labelIntervalY;
            for (double wy = startY; wy >= wBottom - 0.001; wy -= labelIntervalY)
            {
                if (wy < 0) continue;
                double cy = ToCanvasY(wy);
                if (cy < CanvasTop || cy > CanvasBottom) continue;
                string text = FormatLabel(wy);
                var tb = MakeLabel(text, brush, font);
                Canvas.SetLeft(tb, text.Length >= 2 ? 4.0 : 10.0);
                Canvas.SetTop(tb, cy - 4.5);
                AxisLabelCanvas.Children.Add(tb);
            }

            // X labels (bottom)
            double wLeft  = ToWorldX(CanvasLeft);
            double wRight = ToWorldX(CanvasRight);
            double startX = Math.Ceiling(wLeft / labelIntervalX) * labelIntervalX;
            for (double wx = startX; wx <= wRight + 0.001; wx += labelIntervalX)
            {
                double cx = ToCanvasX(wx);
                if (cx < CanvasLeft || cx > CanvasRight) continue;
                string text = FormatLabel(wx);
                var tb = MakeLabel(text, brush, font);
                Canvas.SetLeft(tb, cx - text.Length * 2.8);
                Canvas.SetTop(tb, 443.0);
                AxisLabelCanvas.Children.Add(tb);
            }
        }

        private double GetLabelIntervalY() => _mapScale >= 24 ? 1.0 : _mapScale >= 12 ? 2.0 : _mapScale >= 6 ? 5.0 : 10.0;
        private double GetLabelIntervalX() => GetLabelIntervalY() * 0.5;

        private static TextBlock MakeLabel(string text, SolidColorBrush brush, FontFamily font) =>
            new() { Text = text, Foreground = brush, FontFamily = font, FontSize = 9 };

        private static string FormatLabel(double val)
        {
            double v = Math.Round(val, 3);
            return v == Math.Floor(v) ? ((int)v).ToString() : v.ToString("0.##");
        }

        // ── Origin dot ────────────────────────────────────────────────
        private void UpdateOriginDot()
        {
            double cx = ToCanvasX(0);
            double cy = ToCanvasY(0);
            bool inside = cx >= CanvasLeft && cx <= CanvasRight &&
                          cy >= CanvasTop  && cy <= CanvasBottom;
            OriginDot.Visibility = inside ? Visibility.Visible : Visibility.Hidden;
            if (inside)
            {
                Canvas.SetLeft(OriginDot, cx - 4);
                Canvas.SetTop(OriginDot,  cy - 4);
            }
        }

        // ── Title ─────────────────────────────────────────────────────
        private void UpdateTitle()
        {
            double yMin = Math.Max(0, ToWorldY(CanvasBottom));
            double yMax = ToWorldY(CanvasTop);
            double xMin = ToWorldX(CanvasLeft);
            double xMax = ToWorldX(CanvasRight);
            MapTitleText.Text =
                $"▸ 전역 맵  (전방 {FormatLabel(yMin)}~{FormatLabel(yMax)}m · X {FormatLabel(xMin)}~{FormatLabel(xMax)}m · 격자 X:{FormatLabel(GetGridIntervalX())}m Y:{FormatLabel(GetGridIntervalY())}m)";
        }

        // ── Torpedo ──────────────────────────────────────────────────
        private void RefreshTorpedo()
        {
            if (_vm == null) return;
            double cx = Math.Clamp(ToCanvasX(_vm.TorpedoWorldX), CanvasLeft, CanvasRight);
            double cy = Math.Clamp(ToCanvasY(_vm.TorpedoWorldY), CanvasTop,  CanvasBottom);
            double headRad = _vm.TorpedoHeadingDeg * Math.PI / 180.0;
            const double arrowLen = 22;
            double ax = cx + Math.Sin(headRad) * arrowLen;
            double ay = cy - Math.Cos(headRad) * arrowLen;

            Canvas.SetLeft(TorpedoCanvas, cx - 7);
            Canvas.SetTop(TorpedoCanvas,  cy - 7);

            TorpedoHeadingLine.X1 = 7; TorpedoHeadingLine.Y1 = 7;
            TorpedoHeadingLine.X2 = 7 + (ax - cx);
            TorpedoHeadingLine.Y2 = 7 + (ay - cy);

            double perpX   = -Math.Cos(headRad) * 4;
            double perpY   = -Math.Sin(headRad) * 4;
            double localAx = ax - cx + 7;
            double localAy = ay - cy + 7;
            TorpedoArrow.Points = new PointCollection
            {
                new Point(localAx, localAy),
                new Point(localAx - Math.Sin(headRad) * 8 + perpX, localAy + Math.Cos(headRad) * 8 + perpY),
                new Point(localAx - Math.Sin(headRad) * 6,         localAy + Math.Cos(headRad) * 6),
                new Point(localAx - Math.Sin(headRad) * 8 - perpX, localAy + Math.Cos(headRad) * 8 - perpY),
            };

            Canvas.SetLeft(TorpedoLabel, 16);
            Canvas.SetTop(TorpedoLabel,  3);

            if (_vm.HasSelectedPoint) RefreshAimLine(cx, cy);
            UpdateInfoOverlay();
        }

        // ── Target ───────────────────────────────────────────────────
        private void RefreshTarget()
        {
            if (_vm == null) return;
            double cx = Math.Clamp(ToCanvasX(_vm.TargetWorldX), CanvasLeft, CanvasRight);
            double cy = Math.Clamp(ToCanvasY(_vm.TargetWorldY), CanvasTop,  CanvasBottom);
            Canvas.SetLeft(TargetMarker, cx - 10);
            Canvas.SetTop(TargetMarker,  cy - 9);
            Canvas.SetLeft(TargetLabel,  cx - 6);
            Canvas.SetTop(TargetLabel,   cy + 10);
            UpdateInfoOverlay();
        }

        // ── Selected ─────────────────────────────────────────────────
        private void RefreshSelectedMarker()
        {
            if (_vm == null) return;
            if (!_vm.HasSelectedPoint)
            {
                SelectedCanvas.Visibility = Visibility.Hidden;
                AimLine.Opacity = 0;
                return;
            }
            double cx = Math.Clamp(ToCanvasX(_vm.SelectedWorldX), CanvasLeft, CanvasRight);
            double cy = Math.Clamp(ToCanvasY(_vm.SelectedWorldY), CanvasTop,  CanvasBottom);
            Canvas.SetLeft(SelectedCanvas, cx);
            Canvas.SetTop(SelectedCanvas,  cy);
            SelectedCanvas.Visibility = Visibility.Visible;

            double torpCx = Math.Clamp(ToCanvasX(_vm.TorpedoWorldX), CanvasLeft, CanvasRight);
            double torpCy = Math.Clamp(ToCanvasY(_vm.TorpedoWorldY), CanvasTop,  CanvasBottom);
            RefreshAimLine(torpCx, torpCy);
        }

        private void RefreshAimLine(double torpCx, double torpCy)
        {
            if (_vm == null || !_vm.HasSelectedPoint) return;
            AimLine.X1 = torpCx; AimLine.Y1 = torpCy;
            AimLine.X2 = Math.Clamp(ToCanvasX(_vm.SelectedWorldX), CanvasLeft, CanvasRight);
            AimLine.Y2 = Math.Clamp(ToCanvasY(_vm.SelectedWorldY), CanvasTop,  CanvasBottom);
            AimLine.Opacity = 0.7;
        }

        // ── Obstacles ────────────────────────────────────────────────
        private void RefreshObstacles()
        {
            if (_vm == null) return;
            ObstacleCanvas.Children.Clear();
            foreach (var obstacle in _vm.CurrentObstacles)
            {
                foreach (var pt in obstacle)
                {
                    double cx = ToCanvasX(pt.WorldX);
                    double cy = ToCanvasY(pt.WorldY);
                    if (cx < CanvasLeft || cx > CanvasRight || cy < CanvasTop || cy > CanvasBottom) continue;
                    var dot = new Rectangle
                    {
                        Width = 5, Height = 5,
                        Fill = new SolidColorBrush(Color.FromRgb(0xff, 0xaa, 0x00)),
                        Opacity = 0.9
                    };
                    Canvas.SetLeft(dot, cx - 2.5);
                    Canvas.SetTop(dot,  cy - 2.5);
                    ObstacleCanvas.Children.Add(dot);
                }
            }
            UpdateInfoOverlay();
        }

        // ── Path ─────────────────────────────────────────────────────
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
            if (_vm == null) return;
            var pos = e.GetPosition(MapCanvas);
            double cx = pos.X;
            double cy = pos.Y;

            // Torpedo hit-test — center is TorpedoCanvas origin + (7,7)
            double torpCx = Canvas.GetLeft(TorpedoCanvas) + 7;
            double torpCy = Canvas.GetTop(TorpedoCanvas)  + 7;
            if (Dist(cx, cy, torpCx, torpCy) <= 12)
            {
                _infoTarget = InfoTarget.Torpedo;
                ShowInfo(cx, cy,
                    "▸ 어뢰 (TRP)", Color.FromRgb(0x00, 0xff, 0x88),
                    $"X : {_vm.TorpedoWorldX:F2} m",
                    $"Y : {_vm.TorpedoWorldY:F2} m");
                return;
            }

            // Target hit-test — TargetMarker origin is (cx-10, cy-9), center at +10/+9
            double tgtCx = Canvas.GetLeft(TargetMarker) + 10;
            double tgtCy = Canvas.GetTop(TargetMarker)  + 9;
            if (Dist(cx, cy, tgtCx, tgtCy) <= 14)
            {
                _infoTarget = InfoTarget.Target;
                ShowInfo(cx, cy,
                    "▸ 목표물 (TGT)", Color.FromRgb(0xff, 0x55, 0x55),
                    $"X    : {_vm.TargetWorldX:F2} m",
                    $"Y    : {_vm.TargetWorldY:F2} m",
                    $"속도 : {_vm.TargetSpeedMs:F2} m/s");
                return;
            }

            // Obstacle hit-test — iterate world points and compute canvas positions
            foreach (var cluster in _vm.CurrentObstacles)
            {
                foreach (var pt in cluster)
                {
                    double ocx = ToCanvasX(pt.WorldX);
                    double ocy = ToCanvasY(pt.WorldY);
                    if (Dist(cx, cy, ocx, ocy) <= 8)
                    {
                        _infoTarget         = InfoTarget.Obstacle;
                        _infoObstacleWorldX = pt.WorldX;
                        _infoObstacleWorldY = pt.WorldY;
                        ShowInfo(cx, cy,
                            "▸ 장애물", Color.FromRgb(0xff, 0xaa, 0x00),
                            $"X : {pt.WorldX:F2} m",
                            $"Y : {pt.WorldY:F2} m");
                        return;
                    }
                }
            }

            // No object hit — dismiss overlay
            _infoTarget = InfoTarget.None;
            InfoOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowInfo(double cx, double cy, string title, Color titleColor,
            string line1, string line2, string? line3 = null, string? line4 = null)
        {
            InfoTypeLabel.Text       = title;
            InfoTypeLabel.Foreground = new SolidColorBrush(titleColor);
            InfoLine1.Text = line1;
            InfoLine2.Text = line2;
            InfoLine3.Text       = line3 ?? string.Empty;
            InfoLine3.Visibility = line3 != null ? Visibility.Visible : Visibility.Collapsed;
            InfoLine4.Text       = line4 ?? string.Empty;
            InfoLine4.Visibility = line4 != null ? Visibility.Visible : Visibility.Collapsed;

            const double W = 136, H = 90;
            double left = Math.Clamp(cx + 14, CanvasLeft, CanvasRight  - W);
            double top  = Math.Clamp(cy - 10, CanvasTop,  CanvasBottom - H);
            Canvas.SetLeft(InfoOverlay, left);
            Canvas.SetTop(InfoOverlay,  top);
            InfoOverlay.Visibility = Visibility.Visible;
        }

        private void UpdateInfoOverlay()
        {
            if (_vm == null || InfoOverlay.Visibility != Visibility.Visible) return;
            switch (_infoTarget)
            {
                case InfoTarget.Torpedo:
                    InfoLine1.Text = $"X : {_vm.TorpedoWorldX:F2} m";
                    InfoLine2.Text = $"Y : {_vm.TorpedoWorldY:F2} m";
                    break;
                case InfoTarget.Target:
                    InfoLine1.Text = $"X    : {_vm.TargetWorldX:F2} m";
                    InfoLine2.Text = $"Y    : {_vm.TargetWorldY:F2} m";
                    InfoLine3.Text = $"속도 : {_vm.TargetSpeedMs:F2} m/s";
                    break;
                case InfoTarget.Obstacle:
                    double best = double.MaxValue;
                    double bx = _infoObstacleWorldX, by = _infoObstacleWorldY;
                    foreach (var cluster in _vm.CurrentObstacles)
                        foreach (var pt in cluster)
                        {
                            double d = Dist(pt.WorldX, pt.WorldY, _infoObstacleWorldX, _infoObstacleWorldY);
                            if (d < best) { best = d; bx = pt.WorldX; by = pt.WorldY; }
                        }
                    if (best < 0.5)
                    {
                        InfoLine1.Text      = $"X : {bx:F2} m";
                        InfoLine2.Text      = $"Y : {by:F2} m";
                        _infoObstacleWorldX = bx;
                        _infoObstacleWorldY = by;
                    }
                    break;
            }
        }

        private static double Dist(double x1, double y1, double x2, double y2)
            => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));

        private void MapCanvas_MouseMove(object sender, MouseEventArgs e) { }
    }
}
