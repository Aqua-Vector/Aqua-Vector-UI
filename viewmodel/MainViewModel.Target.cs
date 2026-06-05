using System;
using System.Windows.Threading;
using AquaVectorUI.communication;
using AquaVectorUI.models;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        internal readonly TargetObject CurrentTarget = new();
        private DispatcherTimer? _targetTimer;
        private UdpTargetSender? _targetSender;
        private int _sendLogTick;

        private const double TargetTimerIntervalSec = 0.1; // 100ms = 10Hz
        private const int TargetUdpSendPort = 4001;

        internal void StartTargetTracking()
        {
            // 초기 위치만 바인딩에 반영 — 타이머는 발사 후 시작
            TargetWorldX     = CurrentTarget.X;
            TargetWorldY     = CurrentTarget.Y;
            TargetHeadingDeg = CurrentTarget.HeadingDeg;
            TargetSpeedMs    = CurrentTarget.SpeedMs;

            _targetTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(TargetTimerIntervalSec)
            };
            _targetTimer.Tick += (s, e) =>
            {
                CurrentTarget.Advance(TargetTimerIntervalSec);

                TargetWorldX     = CurrentTarget.X;
                TargetWorldY     = CurrentTarget.Y;
                TargetHeadingDeg = CurrentTarget.HeadingDeg;
                TargetSpeedMs    = CurrentTarget.SpeedMs;

                TargetPositionUpdated?.Invoke(this, EventArgs.Empty);
            };
            // _targetTimer.Start() 은 Fire() 이후에 호출

            StartTargetSender();
        }

        internal void StartTargetMovement()
        {
            _targetTimer?.Start();
        }

        private void StartTargetSender()
        {
            try
            {
                _targetSender = new UdpTargetSender();
                //_targetSender.PacketSent += (seq, x, y) =>
                //{
                //    if (seq % 500 == 0) AppendLog($"[UDP TX] 목표 seq={seq}  ({x:F2}, {y:F2})");
                //};
                _targetSender.Error += msg => AppendLog($"[UDP TX] {msg}");
                _targetSender.Start(IpAddress, TargetUdpSendPort,
                    () => ((float)TargetWorldX, (float)TargetWorldY));
                AppendLog($"[UDP TX] 목표 좌표 송신 시작 → {IpAddress}:{TargetUdpSendPort} (50 Hz)");
            }
            catch (Exception ex)
            {
                AppendLog($"[UDP TX] 시작 실패: {ex.Message}");
            }
        }

        internal void StopTargetTracking()
        {
            _targetTimer?.Stop();
            _targetTimer = null;
            _ = _targetSender?.StopAsync();
            _targetSender = null;
        }
    }
}
