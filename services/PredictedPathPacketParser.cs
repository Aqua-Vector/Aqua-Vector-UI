using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AquaVectorUI.models;

namespace AquaVectorUI.services
{
    public class PredictedPathPacketEventArgs : EventArgs
    {
        public PredictedPathPacket Packet { get; init; } = null!;
    }

    // Packet layout (little-endian):
    //   sync  (1B)  0xAC
    //   seq   (2B)  uint16
    //   count (2B)  uint16 — number of (x, y) pairs
    //   x₀    (4B)  float32  meters
    //   y₀    (4B)  float32  meters
    //   ...
    //   xₙ    (4B)  float32  meters
    //   yₙ    (4B)  float32  meters
    //   crc16 (2B)  CRC-16/CCITT (poly=0x1021, init=0xFFFF), covers all bytes before CRC
    public class PredictedPathPacketParser
    {
        private const byte SyncByte      = 0xAC;
        private const int  HeaderSize    = 5;  // sync(1) + seq(2) + count(2)
        private const int  FooterSize    = 2;
        private const int  MinPacketSize = HeaderSize + FooterSize;

        public event EventHandler<PredictedPathPacketEventArgs>? PacketParsed;
        public event EventHandler<string>? ParseError;

        public void Parse(byte[] data)
        {
            try
            {
                if (data.Length < MinPacketSize)
                {
                    RaiseError($"Too short: {data.Length} B (min {MinPacketSize} B)");
                    return;
                }

                var span = data.AsSpan();

                if (span[0] != SyncByte)
                {
                    RaiseError($"Bad sync: 0x{span[0]:X2}");
                    return;
                }

                ushort rxCrc       = BinaryPrimitives.ReadUInt16LittleEndian(span[^2..]);
                ushort computedCrc = Crc16Ccitt(span[..^2]);
                if (computedCrc != rxCrc)
                {
                    RaiseError($"CRC mismatch: expected 0x{rxCrc:X4}, computed 0x{computedCrc:X4}");
                    return;
                }

                ushort seq   = BinaryPrimitives.ReadUInt16LittleEndian(span[1..3]);
                ushort count = BinaryPrimitives.ReadUInt16LittleEndian(span[3..5]);

                int expectedLen = HeaderSize + count * 8 + FooterSize;
                if (data.Length != expectedLen)
                {
                    RaiseError($"Size mismatch: expected {expectedLen} B for {count} points, got {data.Length} B");
                    return;
                }

                var points = new List<(float X, float Y)>(count);
                int offset = HeaderSize;
                for (int i = 0; i < count; i++)
                {
                    float x = BinaryPrimitives.ReadSingleLittleEndian(span[offset..(offset + 4)]);
                    float y = BinaryPrimitives.ReadSingleLittleEndian(span[(offset + 4)..(offset + 8)]);
                    points.Add((x, y));
                    offset += 8;
                }

                PacketParsed?.Invoke(this, new PredictedPathPacketEventArgs
                {
                    Packet = new PredictedPathPacket { Seq = seq, Points = points }
                });
            }
            catch (Exception ex)
            {
                RaiseError($"Exception: {ex.Message}");
            }
        }

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

        private void RaiseError(string msg) => ParseError?.Invoke(this, $"[PathPacket] {msg}");
    }
}
