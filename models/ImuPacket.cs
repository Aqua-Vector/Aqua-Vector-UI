namespace AquaVectorUI.models
{
    // Torpedo self-reported position calculated from on-board IMU (Zynq → PC, UDP)
    public class TorpedoPacket
    {
        public ushort Seq { get; set; }
        public float X    { get; set; }  // meters
        public float Y    { get; set; }  // meters
    }
}
