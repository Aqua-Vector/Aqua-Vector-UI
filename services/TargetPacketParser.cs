using System;
using System.Buffers.Binary;
using AquaVectorUI.models;

namespace AquaVectorUI.services
{
    public class TorpedoPacketEventArgs : EventArgs
    {
        public TorpedoPacket Packet { get; init; } = null!;
    }

    /// <summary>
    /// Binary packet format (little-endian), total 14B fixed:
    ///   sync  (1B)  0xAA
    ///   seq   (2B)  uint16
    ///   type  (1B)  0x02
    ///   x     (4B)  float32  meters
    ///   y     (4B)  float32  meters
    ///   crc16 (2B)  CRC-16/CCITT (poly=0x1021, init=0xFFFF), covers sync..y
    /// </summary>
    public class TargetPacketParser
    {
        private const byte SyncByte = 0xAA;
        private const byte TypeImu  = 0x10;
        private const int  PacketSize = 14;  // 4B header + 8B payload + 2B footer

        public event EventHandler<TorpedoPacketEventArgs>? PacketParsed;
        public event EventHandler<string>?             ParseError;

        public void Parse(byte[] data)
        {
            try
            {
                if (data.Length != PacketSize)
                {
                    RaiseError($"Wrong size: {data.Length}B (expected {PacketSize}B)");
                    return;
                }

                var span = data.AsSpan();

                if (span[0] != SyncByte)
                {
                    RaiseError($"Bad sync: 0x{span[0]:X2}");
                    return;
                }

                if (span[3] != TypeImu)
                {
                    RaiseError($"Unexpected type: 0x{span[3]:X2}");
                    return;
                }

                ushort rxCrc      = BinaryPrimitives.ReadUInt16LittleEndian(span[^2..]);
                ushort computedCrc = Crc16Ccitt(span[..^2]);
                if (computedCrc != rxCrc)
                {
                    RaiseError($"CRC mismatch: expected 0x{rxCrc:X4}, computed 0x{computedCrc:X4}");
                    return;
                }

                var packet = new TorpedoPacket
                {
                    Seq = BinaryPrimitives.ReadUInt16LittleEndian(span[1..3]),
                    X   = BinaryPrimitives.ReadSingleLittleEndian(span[4..8]),
                    Y   = BinaryPrimitives.ReadSingleLittleEndian(span[8..12]),
                };

                PacketParsed?.Invoke(this, new TorpedoPacketEventArgs { Packet = packet });
            }
            catch (Exception ex)
            {
                RaiseError($"Exception: {ex.Message}");
            }
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

        private void RaiseError(string msg) => ParseError?.Invoke(this, $"[ImuPacket] {msg}");
    }
}
