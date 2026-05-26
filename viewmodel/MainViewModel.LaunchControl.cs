using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows;
using AquaVectorUI.services;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        [RelayCommand]
        public async Task OpenDoor()
        {
            byte[] packet = CommandPacketBuilder.Build(NextCmdSeq(), PacketType.Door, true);
            await TransmitBytes(packet);
            IsDoorOpen     = true;
            DoorStatusText = "OPEN";
            AppendLog("[TCP] 도어 개방 명령 전송");
        }

        [RelayCommand]
        public async Task CloseDoor()
        {
            byte[] packet = CommandPacketBuilder.Build(NextCmdSeq(), PacketType.Door, false);
            await TransmitBytes(packet);
            IsDoorOpen     = false;
            DoorStatusText = "CLOSED";
            AppendLog("[TCP] 도어 폐쇄 명령 전송");
        }

        [RelayCommand]
        public async Task SetAzimuth()
        {
            await Transmit($"CMD:AZIMUTH:{AzimuthDeg:F1}");
            AppendLog($"명령: 방위각 설정 → {AzimuthDeg:F1}°");
        }

        [RelayCommand]
        public async Task SetElevation()
        {
            await Transmit($"CMD:ELEVATION:{ElevationDeg:F1}");
            AppendLog($"명령: 고각 설정 → {ElevationDeg:F1}°");
        }

        private bool CanAimAtTarget() => HasSelectedPoint;

        [RelayCommand(CanExecute = nameof(CanAimAtTarget))]
        public async Task AimAtTarget()
        {
            double az = ComputeAzimuth(TorpedoWorldX, TorpedoWorldY, SelectedWorldX, SelectedWorldY);
            AzimuthDeg = System.Math.Round(az, 1);
            await Transmit($"CMD:AIM:{SelectedWorldX:F1},{SelectedWorldY:F1}");
            AppendLog($"목표 조준: ({SelectedWorldX:F1}m, {SelectedWorldY:F1}m) → 방위각 {AzimuthDeg:F1}°");
        }

        private bool CanFire() => IsConnected && IsTorpedoOnline && IsDoorOpen;

        [RelayCommand(CanExecute = nameof(CanFire))]
        public async Task Fire()
        {
            var result = MessageBox.Show(
                "어뢰를 발사하시겠습니까?\n\n이 명령은 즉시 실행됩니다.",
                "발사 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            byte[] packet = CommandPacketBuilder.Build(NextCmdSeq(), PacketType.Fire, true);
            await TransmitBytes(packet);
            AppendLog($"[TCP] 어뢰 발사 명령 전송");
            StartTargetMovement();
        }

        [RelayCommand]
        public async Task TerminalGuidance()
        {
            var result = MessageBox.Show(
                "종말 유도 명령을 전송하시겠습니까?\n\n이 명령은 즉시 실행됩니다.",
                "종말 유도 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            byte[] packet = CommandPacketBuilder.Build(NextCmdSeq(), PacketType.TerminalGuidance, true);
            await TransmitBytes(packet);
            AppendLog("[TCP] 종말 유도 명령 전송");
        }
    }
}
