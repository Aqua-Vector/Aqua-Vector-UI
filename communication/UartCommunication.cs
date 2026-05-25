using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace AquaVectorUI.communication
{
    public class UartCommunication : ICommunication
    {
        private SerialPort? _port;
        private readonly List<byte> _rxBuffer = new();

        private readonly string _portName;
        private readonly int _baudRate;

        // TorpedoPacket: sync(1)+seq(2)+type(1=0x02)+x(4)+y(4)+crc(2) = 14B fixed
        private const int TorpedoPacketSize = 14;
        // LiDAR header: sync(1)+seq(2)+obj_count(2)+torpedo_x(4)+torpedo_y(4)+yaw(4) = 17B
        private const int LidarHeaderSize = 17;

        public event Action<string>? OnDataReceived;

        // Fires with a complete, CRC-validated binary packet (byte[]).
        // Subscribe in the ViewModel after casting to UartCommunication.
        public event Action<byte[]>? OnBinaryPacketReceived;

        // Fires when a packet is dropped during framing (e.g. CRC mismatch).
        public event Action<string>? OnParseError;

        public bool IsConnected => _port?.IsOpen ?? false;

        public UartCommunication(string portName, int baudRate)
        {
            _portName = portName;
            _baudRate = baudRate;
        }

        public Task ConnectAsync()
        {
            _port = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One);
            _port.DataReceived += SerialPort_DataReceived;
            _port.Open();
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            _port?.Close();
            return Task.CompletedTask;
        }

        public Task SendAsync(string data)
        {
            if (_port != null && _port.IsOpen)
                _port.WriteLine(data);
            return Task.CompletedTask;
        }

        public Task SendBytesAsync(byte[] data)
        {
            if (_port != null && _port.IsOpen)
                _port.BaseStream.Write(data, 0, data.Length);
            return Task.CompletedTask;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int available = _port!.BytesToRead;
            byte[] incoming = new byte[available];
            _port.Read(incoming, 0, available);
            _rxBuffer.AddRange(incoming);
            ProcessBuffer();
        }

        // Extracts complete packets from _rxBuffer using sync-byte (0xAA) framing.
        // Packet types are distinguished by byte[3]:
        //   0x02 → TorpedoPacket (14B fixed)
        //   else → LiDAR packet  (variable: 17B header + objects + 2B CRC)
        private void ProcessBuffer()
        {
            while (true)
            {
                // Align to sync byte
                int syncIdx = _rxBuffer.IndexOf(0xAA);
                if (syncIdx < 0) { _rxBuffer.Clear(); return; }
                if (syncIdx > 0) _rxBuffer.RemoveRange(0, syncIdx);

                if (_rxBuffer.Count < 5) return; // need at least sync+seq(2)+byte[3]+byte[4]

                int packetLen;

                if (_rxBuffer[3] == 0x02)
                {
                    // TorpedoPacket — fixed size
                    packetLen = TorpedoPacketSize;
                }
                else
                {
                    // LiDAR packet — variable: walk object list to find total length
                    if (_rxBuffer.Count < 6) return;
                    ushort objCount = (ushort)(_rxBuffer[3] | (_rxBuffer[4] << 8));

                    int offset = LidarHeaderSize;
                    bool needMore = false;
                    for (int i = 0; i < objCount; i++)
                    {
                        if (_rxBuffer.Count < offset + 2) { needMore = true; break; }
                        ushort ptCount = (ushort)(_rxBuffer[offset] | (_rxBuffer[offset + 1] << 8));
                        offset += 2 + ptCount * 8;
                    }
                    if (needMore) return;

                    packetLen = offset + 2; // +2 for CRC footer
                }

                if (_rxBuffer.Count < packetLen) return; // wait for more bytes

                // Extract candidate packet and validate CRC
                byte[] packet  = _rxBuffer.Take(packetLen).ToArray();
                ushort rxCrc   = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(packetLen - 2));
                ushort calcCrc = Crc16Ccitt(packet.AsSpan(0, packetLen - 2));

                if (rxCrc != calcCrc)
                {
                    string pktType = packet[3] == 0x02 ? "Torpedo(0x02)" : $"LiDAR(0x{packet[3]:X2})";
                    OnParseError?.Invoke(
                        $"CRC 불일치 [{pktType}, {packetLen}B] — 수신: 0x{rxCrc:X4}, 계산: 0x{calcCrc:X4}, " +
                        $"헤더: {string.Join(" ", packet.Take(Math.Min(6, packet.Length)).Select(b => $"{b:X2}"))}");
                    _rxBuffer.RemoveAt(0);
                    continue;
                }

                _rxBuffer.RemoveRange(0, packetLen);
                OnBinaryPacketReceived?.Invoke(packet);
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
    }
}
