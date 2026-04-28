using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using AquaVectorUI.models;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        private bool CanStartRecording() => !IsRecordingPath;

        [RelayCommand(CanExecute = nameof(CanStartRecording))]
        public async Task StartRecording()
        {
            PathPoints.Clear();
            IsRecordingPath = true;
            PathStatusText = "기록 중...";
            await Transmit("CMD:PATH:RECORD:START");
            AppendLog("▶ 경로 기록 시작");
        }

        private bool CanStopRecording() => IsRecordingPath;

        [RelayCommand(CanExecute = nameof(CanStopRecording))]
        public async Task StopRecording()
        {
            IsRecordingPath = false;
            PathStatusText = $"기록 완료 ({PathPoints.Count}점)";
            await Transmit("CMD:PATH:RECORD:STOP");
            AppendLog($"⏹ 경로 기록 완료 ({PathPoints.Count}점)");
        }

        private bool CanPlaybackPath() => !IsRecordingPath && PathPoints.Count > 0;

        [RelayCommand(CanExecute = nameof(CanPlaybackPath))]
        public void PlaybackPath()
        {
            if (PathPoints.Count == 0) return;
            PlaybackIndex = 0;
            PlaybackTimer?.Stop();
            PlaybackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            PlaybackTimer.Tick += (s, e) =>
            {
                if (PlaybackIndex >= PathPoints.Count)
                {
                    PlaybackTimer.Stop();
                    PathStatusText = $"재생 완료 ({PathPoints.Count}점)";
                    AppendLog("⏪ 경로 재생 완료");
                    return;
                }
                PathPoint pt = PathPoints[PlaybackIndex++];
                TorpedoWorldX = pt.WorldX;
                TorpedoWorldY = pt.WorldY;
                TorpedoHeadingDeg = pt.HeadingDeg;
                TorpedoPositionUpdated?.Invoke(this, EventArgs.Empty);
                PathStatusText = $"재생 중... ({PlaybackIndex}/{PathPoints.Count})";
            };
            PlaybackTimer.Start();
            AppendLog($"⏪ 경로 재생 중 ({PathPoints.Count}점)...");
        }

        [RelayCommand]
        public void ClearPath()
        {
            PlaybackTimer?.Stop();
            PathPoints.Clear();
            PathStatusText = "기록 없음";
            PlaybackPathCommand.NotifyCanExecuteChanged();
            AppendLog("경로 삭제됨");
        }
    }
}
