using System;
using System.Buffers.Binary;

namespace AquaVectorUI.services
{
    // Packet layout (PC → Zynq, TCP), 7B fixed:
    //   sync     (1B)  0xBB
    //   seq      (2B)  uint16 LE
    //   type     (1B)  0x11=개폐 / 0x12=발사 / 0x13=종말유도
    //   cmd_data (1B)  0x01=True / 0x00=False
    //   crc16    (2B)  CRC-16/CCITT over bytes 0–4
    public enum PacketType : byte
    {
        Door             = 0x11,
        Fire             = 0x12,
        TerminalGuidance = 0x13,
    }

    public static class CommandPacketBuilder
    {
        private const byte Sync = 0xBB;

        public static byte[] Build(ushort seq, PacketType type, bool data)
        {
            Span<byte> buf = stackalloc byte[7];
            buf[0] = Sync;
            BinaryPrimitives.WriteUInt16LittleEndian(buf[1..3], seq);
            buf[3] = (byte)type;
            buf[4] = data ? (byte)0x01 : (byte)0x00;
            BinaryPrimitives.WriteUInt16LittleEndian(buf[5..7], Crc16Ccitt(buf[..5]));
            return buf.ToArray();
        }

        // CRC-16/CCITT: poly=0x1021, init=0xFFFF, no bit-reflection
        private static ushort Crc16Ccitt(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
            return crc;
        }
    }
}
