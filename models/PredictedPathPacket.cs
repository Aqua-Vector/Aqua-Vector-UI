using System.Collections.Generic;

namespace AquaVectorUI.models
{
    public class PredictedPathPacket
    {
        public ushort Seq { get; init; }
        public List<(float X, float Y)> Points { get; init; } = new();
    }
}
