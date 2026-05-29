using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AquaVectorUI.views
{
    public partial class AcousticSpectrumControl : UserControl
    {
        private const double FStart = 100.0;
        private const double FEnd = 3000.0;
        private const double TargetFreq = 500.0;
        private const double SecondaryTargetFreq = 700.0;
        private const double ToneTrackWindowHz = 80.0;
        private const double DisplaySmoothing = 0.25;
        private const double DirectionSmoothing = 0.18;
        private static readonly TimeSpan StaleTimeout = TimeSpan.FromSeconds(2);
        private const int SpectrumBins = 128;
        private const int LegacyPacketFloats = 134;
        private const int DualPacketFloats = SpectrumBins * 2 + 6;
        private const int LegacySnrIndex = 128;
        private const int LegacyPatternIndex = 129;
        private const int LegacyLeftTargetIndex = 130;
        private const int LegacyRightTargetIndex = 131;
        private const int LegacyDirectionIndex = 132;
        private const int LegacyConfidenceIndex = 133;
        private const int SnrIndex = SpectrumBins * 2;
        private const int PatternIndex = SnrIndex + 1;
        private const int LeftTargetIndex = SnrIndex + 2;
        private const int RightTargetIndex = SnrIndex + 3;
        private const int DirectionIndex = SnrIndex + 4;
        private const int ConfidenceIndex = SnrIndex + 5;

        private readonly double[] _leftSpectrum = new double[SpectrumBins];
        private readonly double[] _rightSpectrum = new double[SpectrumBins];
        private bool _hasLeftSpectrum;
        private bool _hasRightSpectrum;
        private UdpClient? _udpClient;
        private DispatcherTimer? _statusTimer;
        private DateTime _lastPacketAtUtc = DateTime.MinValue;
        private DateTime _monitoringEnabledAtUtc = DateTime.MinValue;
        private bool _lastLocked;
        private bool _hasSmoothedDirection;
        private double _smoothedDirection;
        private bool _running;

        public static readonly DependencyProperty IsMonitoringEnabledProperty =
            DependencyProperty.Register(
                nameof(IsMonitoringEnabled),
                typeof(bool),
                typeof(AcousticSpectrumControl),
                new PropertyMetadata(false, OnIsMonitoringEnabledChanged));

        public bool IsMonitoringEnabled
        {
            get => (bool)GetValue(IsMonitoringEnabledProperty);
            set => SetValue(IsMonitoringEnabledProperty, value);
        }

        public AcousticSpectrumControl()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_udpClient != null) return;

            try
            {
                _running = true;
                _udpClient = new UdpClient(5555);
                SetWaitingStatus();
                _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _statusTimer.Tick += StatusTimer_Tick;
                _statusTimer.Start();
                _ = Task.Run(ReceiveLoop);
            }
            catch (SocketException)
            {
                StatusLabel.Text = "PORT BUSY";
                StatusLabel.Foreground = Brushes.OrangeRed;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _running = false;
            _statusTimer?.Stop();
            _statusTimer = null;
            _udpClient?.Close();
            _udpClient = null;
        }

        private async Task ReceiveLoop()
        {
            while (_running && _udpClient != null)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    float[] packet = new float[result.Buffer.Length / sizeof(float)];
                    Buffer.BlockCopy(result.Buffer, 0, packet, 0, result.Buffer.Length);
                    Dispatcher.Invoke(() => UpdateUi(packet));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }
                catch
                {
                    // Keep the analyzer alive if one malformed UDP packet arrives.
                }
            }
        }

        private void UpdateUi(float[] data)
        {
            if (data.Length < LegacyPacketFloats) return;
            if (!IsMonitoringEnabled) return;

            _lastPacketAtUtc = DateTime.UtcNow;
            bool hasDualSpectrum = data.Length >= DualPacketFloats;
            int snrIndex = hasDualSpectrum ? SnrIndex : LegacySnrIndex;
            int patternIndex = hasDualSpectrum ? PatternIndex : LegacyPatternIndex;
            int leftTargetIndex = hasDualSpectrum ? LeftTargetIndex : LegacyLeftTargetIndex;
            int rightTargetIndex = hasDualSpectrum ? RightTargetIndex : LegacyRightTargetIndex;
            int directionIndex = hasDualSpectrum ? DirectionIndex : LegacyDirectionIndex;
            int confidenceIndex = hasDualSpectrum ? ConfidenceIndex : LegacyConfidenceIndex;

            double width = Math.Max(1.0, SpectrumCanvas.ActualWidth);
            double height = Math.Max(1.0, SpectrumCanvas.ActualHeight);
            var leftPoints = new PointCollection(SpectrumBins);
            var rightPoints = new PointCollection(SpectrumBins);

            for (int i = 0; i < SpectrumBins; i++)
            {
                double x = i / 127.0 * width;
                double leftValue = Smooth(data[i], _leftSpectrum, i, _hasLeftSpectrum);
                leftPoints.Add(new Point(x, DbToY(leftValue, height)));

                if (hasDualSpectrum)
                {
                    double rightValue = Smooth(data[SpectrumBins + i], _rightSpectrum, i, _hasRightSpectrum);
                    rightPoints.Add(new Point(x, DbToY(rightValue, height)));
                }
            }

            _hasLeftSpectrum = true;
            SpectrumLineLeft.Points = leftPoints;

            if (hasDualSpectrum)
            {
                _hasRightSpectrum = true;
                SpectrumLineRight.Visibility = Visibility.Visible;
                SpectrumLineRight.Points = rightPoints;
            }
            else
            {
                SpectrumLineRight.Visibility = Visibility.Hidden;
                SpectrumLineRight.Points = new PointCollection();
            }

            bool locked = SafeValue(data, patternIndex) > 0.5;
            SetFrequencyGuide(TargetGuide, TargetFreq, width, height);
            SetFrequencyGuide(SecondaryTargetGuide, SecondaryTargetFreq, width, height);
            SetFrequencyLabels(width);

            double targetTrackFreq = locked ? TargetFreq : FindTonePeakFreq(TargetFreq, hasDualSpectrum);
            double secondaryTrackFreq = locked ? SecondaryTargetFreq : FindTonePeakFreq(SecondaryTargetFreq, hasDualSpectrum);
            SetFrequencyGuide(TargetTrackingGuide, targetTrackFreq, width, height);
            SetFrequencyGuide(SecondaryTrackingGuide, secondaryTrackFreq, width, height);
            SetTrackTag(TargetTrackTag, targetTrackFreq, width, 2.0, 0.0);
            SetTrackTag(SecondaryTrackTag, secondaryTrackFreq, width, 2.0, 0.0);

            double snr = SafeValue(data, snrIndex);
            double leftTarget = SafeValue(data, leftTargetIndex);
            double rightTarget = hasDualSpectrum ? SafeValue(data, rightTargetIndex) : -100.0;
            double direction = SmoothDirection(SafeValue(data, directionIndex));
            double confidence = SafeValue(data, confidenceIndex);
            _lastLocked = locked;

            SetLiveStatus();
            MetricsLabel.Text = $"SNR {snr:F1} dB | L {leftTarget:F1} dB | R {rightTarget:F1} dB";
            DirectionLabel.Text = DirectionText(direction);
            PeakLabel.Text = $"Tone peaks {targetTrackFreq:F0}/{secondaryTrackFreq:F0} Hz | Confidence {confidence:F0}%";
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsMonitoringEnabled)
            {
                SetWaitingStatus();
                return;
            }

            DateTime lastActivity = _lastPacketAtUtc != DateTime.MinValue
                ? _lastPacketAtUtc
                : _monitoringEnabledAtUtc;

            if (lastActivity == DateTime.MinValue) return;

            if (DateTime.UtcNow - lastActivity > StaleTimeout)
            {
                StatusLabel.Text = "SIGNAL LOST";
                StatusLabel.Foreground = Brushes.Orange;
            }
        }

        private void SpectrumCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = Math.Max(1.0, e.NewSize.Width);
            double height = Math.Max(1.0, e.NewSize.Height);
            SetGridLine(GridLine20, width, height * 0.2);
            SetGridLine(GridLine40, width, height * 0.4);
            SetGridLine(GridLine60, width, height * 0.6);
            SetGridLine(GridLine80, width, height * 0.8);
            SetFrequencyGuide(TargetGuide, TargetFreq, width, height);
            SetFrequencyGuide(SecondaryTargetGuide, SecondaryTargetFreq, width, height);
            SetFrequencyLabels(width);
        }

        private static double Smooth(double value, double[] buffer, int index, bool initialized)
        {
            if (!initialized)
            {
                buffer[index] = value;
            }
            else
            {
                buffer[index] += (value - buffer[index]) * DisplaySmoothing;
            }
            return buffer[index];
        }

        private static double DbToY(double db, double height)
        {
            return Clamp((db / -100.0) * height, 0.0, height);
        }

        private static double SafeValue(float[] data, int index)
        {
            return index >= 0 && index < data.Length ? data[index] : 0.0;
        }

        private void SetLiveStatus()
        {
            StatusLabel.Text = _lastLocked ? "LOCKED" : "SEARCHING";
            StatusLabel.Foreground = _lastLocked ? Brushes.Lime : Brushes.White;
        }

        private static void OnIsMonitoringEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AcousticSpectrumControl control)
            {
                control.SetMonitoringState((bool)e.NewValue);
            }
        }

        private void SetMonitoringState(bool enabled)
        {
            _lastPacketAtUtc = DateTime.MinValue;
            _monitoringEnabledAtUtc = enabled ? DateTime.UtcNow : DateTime.MinValue;
            _lastLocked = false;
            _hasSmoothedDirection = false;

            if (enabled)
            {
                StatusLabel.Text = "SEARCHING";
                StatusLabel.Foreground = Brushes.White;
            }
            else
            {
                ClearSpectrumDisplay();
                SetWaitingStatus();
            }
        }

        private void SetWaitingStatus()
        {
            StatusLabel.Text = "WAITING";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xa0, 0xb8, 0xcc));
        }

        private void ClearSpectrumDisplay()
        {
            _hasLeftSpectrum = false;
            _hasRightSpectrum = false;
            Array.Clear(_leftSpectrum, 0, _leftSpectrum.Length);
            Array.Clear(_rightSpectrum, 0, _rightSpectrum.Length);
            SpectrumLineLeft.Points = new PointCollection();
            SpectrumLineRight.Points = new PointCollection();
            MetricsLabel.Text = "SNR -- dB | L -- dB | R -- dB";
            DirectionLabel.Text = "CENTER";
            PeakLabel.Text = "Tone peaks --/-- Hz | Confidence --%";
        }

        private double SmoothDirection(double direction)
        {
            direction = Clamp(direction, -40.0, 40.0);
            if (!_hasSmoothedDirection)
            {
                _smoothedDirection = direction;
                _hasSmoothedDirection = true;
            }
            else
            {
                _smoothedDirection += (direction - _smoothedDirection) * DirectionSmoothing;
            }
            return _smoothedDirection;
        }

        private static string DirectionText(double direction)
        {
            string side = "CENTER";
            if (direction > 3.0) side = "LEFT";
            if (direction < -3.0) side = "RIGHT";
            return $"{side} {direction:+0.0;-0.0;0.0}dB";
        }

        private static void SetGridLine(System.Windows.Shapes.Line line, double width, double y)
        {
            line.X1 = 0;
            line.X2 = width;
            line.Y1 = y;
            line.Y2 = y;
        }

        private static void SetFrequencyGuide(System.Windows.Shapes.Line line, double freq, double width, double height)
        {
            double x = FrequencyToX(freq, width);
            line.X1 = line.X2 = Clamp(x, 0.0, width);
            line.Y1 = 0;
            line.Y2 = height;
        }

        private double FindTonePeakFreq(double targetFreq, bool hasRightSpectrum)
        {
            int start = FrequencyToIndex(targetFreq - ToneTrackWindowHz);
            int end = FrequencyToIndex(targetFreq + ToneTrackWindowHz);
            int peakIndex = FrequencyToIndex(targetFreq);
            double peakDb = double.NegativeInfinity;

            for (int i = start; i <= end; i++)
            {
                double value = _leftSpectrum[i];
                if (hasRightSpectrum) value = Math.Max(value, _rightSpectrum[i]);
                if (value > peakDb)
                {
                    peakDb = value;
                    peakIndex = i;
                }
            }

            return IndexToFrequency(peakIndex);
        }

        private static int FrequencyToIndex(double freq)
        {
            int index = (int)Math.Round((freq - FStart) / (FEnd - FStart) * (SpectrumBins - 1));
            if (index < 0) return 0;
            if (index >= SpectrumBins) return SpectrumBins - 1;
            return index;
        }

        private static double IndexToFrequency(int index)
        {
            return FStart + index * (FEnd - FStart) / (SpectrumBins - 1);
        }

        private static double FrequencyToX(double freq, double width)
        {
            return ((freq - FStart) / (FEnd - FStart)) * width;
        }

        private void SetFrequencyLabels(double width)
        {
            SetFrequencyLabel(FStartLabel, FStart, width, 2.0, 0.0);
            SetFrequencyLabel(TargetFreqLabel, TargetFreq, width, 2.0, 0.0);
            SetFrequencyLabel(SecondaryTargetFreqLabel, SecondaryTargetFreq, width, 2.0, 0.0);
            SetFrequencyLabel(FEndLabel, FEnd, width, 2.0, 24.0);

            FrequencyUnitLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(FrequencyUnitLabel, Math.Max(2.0, width - FrequencyUnitLabel.DesiredSize.Width - 2.0));
        }

        private static void SetFrequencyLabel(TextBlock label, double freq, double width, double minLeft, double rightReserve)
        {
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double x = ((freq - FStart) / (FEnd - FStart)) * width;
            double left = x - label.DesiredSize.Width / 2.0;
            Canvas.SetLeft(label, Clamp(left, minLeft, width - label.DesiredSize.Width - rightReserve));
        }

        private static void SetTrackTag(TextBlock tag, double freq, double width, double minLeft, double rightReserve)
        {
            tag.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double left = FrequencyToX(freq, width) - tag.DesiredSize.Width / 2.0;
            Canvas.SetLeft(tag, Clamp(left, minLeft, width - tag.DesiredSize.Width - rightReserve));
        }

        private static double Clamp(double value, double min, double max)
        {
            if (max < min) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
