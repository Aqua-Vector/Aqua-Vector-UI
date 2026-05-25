using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AquaVectorUI.models;

namespace AquaVectorUI.services
{
    public class LidarScanPacketEventArgs : EventArgs
    {
        public LidarScanPacket Packet { get; init; } = null!;
    }

    /// <summary>
    /// Binary packet format (little-endian):
    ///   Header : sync(1) + seq(2) + type(1) + obj_count(2)  = 6 B
    ///            NOTE: user spec says 8 B — verify whether a length/reserved field exists
    ///   Per object : pt_count(2) + pt_count × [x(4) + y(4)]
    ///   Footer : crc16(2)  CRC-16/CCITT (poly=0x1021, init=0xFFFF), covers sync..last payload byte
    /// </summary>
    public class LidarPacketParser
    {
        private const byte SyncByte  = 0xAA;
        // sync(1) + seq(2) + obj_count(2) + torpedo_x(4) + torpedo_y(4) + yaw(4) = 17 B
        private const int  HeaderSize = 17;
        private const int  FooterSize = 2;   // crc16
        private const int  MinPacketSize = HeaderSize + FooterSize;

        public event EventHandler<LidarScanPacketEventArgs>? PacketParsed;
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

                ushort rxCrc      = BinaryPrimitives.ReadUInt16LittleEndian(span[^2..]);
                ushort computedCrc = Crc16Ccitt(span[..^2]);
                if (computedCrc != rxCrc)
                {
                    RaiseError($"CRC mismatch: expected 0x{rxCrc:X4}, computed 0x{computedCrc:X4}");
                    return;
                }

                ushort seq      = BinaryPrimitives.ReadUInt16LittleEndian(span[1..3]);
                ushort objCount = BinaryPrimitives.ReadUInt16LittleEndian(span[3..5]);
                float  torpX    = BinaryPrimitives.ReadSingleLittleEndian(span[5..9]);
                float  torpY    = BinaryPrimitives.ReadSingleLittleEndian(span[9..13]);
                float  yaw      = BinaryPrimitives.ReadSingleLittleEndian(span[13..17]);

                if (float.IsNaN(torpX)) torpX = 0f;
                if (float.IsNaN(torpY)) torpY = 0f;
                if (float.IsNaN(yaw))   yaw   = 0f;

                int offset  = HeaderSize;
                int payloadEnd = data.Length - FooterSize;
                var objects = new List<LidarScanObject>(objCount);

                for (int i = 0; i < objCount; i++)
                {
                    if (offset + 2 > payloadEnd)
                    {
                        RaiseError($"Truncated at object {i}: missing pt_count");
                        return;
                    }

                    ushort ptCount = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..(offset + 2)]);
                    offset += 2;

                    int pointBytes = ptCount * 8; // 4B x + 4B y
                    if (offset + pointBytes > payloadEnd)
                    {
                        RaiseError($"Truncated at object {i}: need {pointBytes} B for {ptCount} points, only {payloadEnd - offset} B left");
                        return;
                    }

                    var points = new List<ObstaclePoint>(ptCount);
                    for (int j = 0; j < ptCount; j++)
                    {
                        float x = BinaryPrimitives.ReadSingleLittleEndian(span[offset..(offset + 4)]);
                        float y = BinaryPrimitives.ReadSingleLittleEndian(span[(offset + 4)..(offset + 8)]);
                        points.Add(new ObstaclePoint { WorldX = x, WorldY = y });
                        offset += 8;
                    }

                    objects.Add(new LidarScanObject { Points = points });
                }

                PacketParsed?.Invoke(this, new LidarScanPacketEventArgs
                {
                    Packet = new LidarScanPacket
                    {
                        Seq     = seq,
                        Torpedo = new Torpedo { X = torpX, Y = torpY, Yaw = yaw },
                        Objects = objects
                    }
                });
            }
            catch (Exception ex)
            {
                RaiseError($"Exception: {ex.Message}");
            }
        }

        // CRC-16/CCITT: poly=0x1021, init=0xFFFF, no bit-reflection (X.25 / ITU-T variant)
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


        private void RaiseError(string msg) => ParseError?.Invoke(this, $"[LidarPacket] {msg}");
    }
}
