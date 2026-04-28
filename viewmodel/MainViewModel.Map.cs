using System;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        // Canvas 500×500, origin (250,250), scale 2px/m.
        public void HandleMapClick(double canvasX, double canvasY)
        {
            SelectedWorldX = Math.Round((canvasX - 250.0) / 2.0, 1);
            SelectedWorldY = Math.Round((250.0 - canvasY) / 2.0, 1);
            HasSelectedPoint = true;
            SelectedCoordText = $"X: {SelectedWorldX:F1}m   Y: {SelectedWorldY:F1}m";

            AzimuthDeg = Math.Round(
                ComputeAzimuth(TorpedoWorldX, TorpedoWorldY, SelectedWorldX, SelectedWorldY), 1);

            SelectedPointUpdated?.Invoke(this, EventArgs.Empty);
            AppendLog($"맵 선택: ({SelectedWorldX:F1}m, {SelectedWorldY:F1}m) → 방위각 {AzimuthDeg:F1}°");
        }

        public void HandleMapHover(double canvasX, double canvasY)
        {
            double wx = (canvasX - 250.0) / 2.0;
            double wy = (250.0 - canvasY) / 2.0;
            HoverCoordText = $"({wx:F0}m, {wy:F0}m)";
        }
    }
}
