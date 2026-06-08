using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using AquaVectorUI.services;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        [ObservableProperty]
        private bool _isFireConfirmVisible;

        [ObservableProperty]
        private bool _isTerminalGuidanceConfirmVisible;

        [ObservableProperty]
        private bool _isTorpedoFired;

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

        // Shows the in-panel overlay; actual firing is in ConfirmFire
        [RelayCommand(CanExecute = nameof(CanFire))]
        public void Fire() => IsFireConfirmVisible = true;

        [RelayCommand]
        public async Task ConfirmFire()
        {
            IsFireConfirmVisible = false;
            byte[] packet = CommandPacketBuilder.Build(NextCmdSeq(), PacketType.Fire, true);
            await TransmitBytes(packet);
            AppendLog("[TCP] 어뢰 발사 명령 전송");
            IsTorpedoFired = true;
            TerminalGuidanceCommand.NotifyCanExecuteChanged();
            StartTargetMovement();
        }

        [RelayCommand]
        public void CancelFireConfirm() => IsFireConfirmVisible = false;

        private bool CanTerminalGuidance() => IsTorpedoFired;

        // Shows the in-panel overlay; actual command is in ConfirmTerminalGuidance
        [RelayCommand(CanExecute = nameof(CanTerminalGuidance))]
        public void TerminalGuidance() => IsTerminalGuidanceConfirmVisible = true;

        [RelayCommand]
        public async Task ConfirmTerminalGuidance()
        {
            IsTerminalGuidanceConfirmVisible = false;
            byte[] packet = CommandPacketBuilder.Build(NextCmdSeq(), PacketType.TerminalGuidance, true);
            await TransmitBytes(packet);
            AppendLog("[TCP] 종말 유도 명령 전송");
        }

        [RelayCommand]
        public void CancelTerminalGuidanceConfirm() => IsTerminalGuidanceConfirmVisible = false;
    }
}
