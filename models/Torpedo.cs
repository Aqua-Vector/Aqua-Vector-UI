namespace AquaVectorUI.models
{
    // Live state of the torpedo, populated from incoming LiDAR/IMU packets.
    public class Torpedo
    {
        public float X   { get; set; }  // meters
        public float Y   { get; set; }  // meters
        public float Yaw { get; set; }  // degrees, -90..90, 0 = forward
    }
}
