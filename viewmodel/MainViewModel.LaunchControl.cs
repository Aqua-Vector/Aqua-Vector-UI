using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        [RelayCommand]
        public async Task OpenDoor()
        {
            await Transmit("CMD:DOOR:OPEN");
            AppendLog("명령: 발사관 도어 열기");
        }

        [RelayCommand]
        public async Task CloseDoor()
        {
            await Transmit("CMD:DOOR:CLOSE");
            AppendLog("명령: 발사관 도어 닫기");
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

        private bool CanFire() => HasSelectedPoint;

        [RelayCommand(CanExecute = nameof(CanFire))]
        public async Task Fire()
        {
            await Transmit($"CMD:FIRE:{SelectedWorldX:F1},{SelectedWorldY:F1}");
            AppendLog($"🚀 어뢰 발사! 목표: ({SelectedWorldX:F1}m, {SelectedWorldY:F1}m)");
        }
    }
}
