using System;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        public void HandleMapClick(double worldX, double worldY)
        {
            // Temporarily disabled
            // SelectedWorldX = Math.Round(worldX, 2);
            // SelectedWorldY = Math.Round(worldY, 2);
            // HasSelectedPoint = true;
            // SelectedCoordText = $"X: {SelectedWorldX:F2}m   Y: {SelectedWorldY:F2}m";
            // AzimuthDeg = Math.Round(
            //     ComputeAzimuth(TorpedoWorldX, TorpedoWorldY, SelectedWorldX, SelectedWorldY), 1);
            // SelectedPointUpdated?.Invoke(this, EventArgs.Empty);
            // AppendLog($"맵 선택: ({SelectedWorldX:F2}m, {SelectedWorldY:F2}m) → 방위각 {AzimuthDeg:F1}°");
        }

        public void HandleMapHover(double worldX, double worldY)
        {
            // Temporarily disabled
            // HoverCoordText = $"({worldX:F2}m, {worldY:F2}m)";
        }
    }
}
