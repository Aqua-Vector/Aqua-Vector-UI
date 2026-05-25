using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AquaVectorUI.communication
{
    // Sends target-coordinate packets to Zynq at 50 Hz over UDP (no connection required).
    // Packet layout (PC → Zynq, 14 B):
    //   sync     (1B)  0xBB
    //   seq      (2B)  uint16 LE, wraps at 65535
    //   type     (1B)  0x10
    //   target_x (4B)  float32 LE
    //   target_y (4B)  float32 LE
    //   crc16    (2B)  CRC-16/CCITT over bytes 0–11
    public class UdpTargetSender : IDisposable
    {
        private UdpClient? _client;
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private ushort _seq;

        public event Action<ushort, float, float>? PacketSent;
        public event Action<string>? Error;

        public bool IsRunning => _cts is { IsCancellationRequested: false };

        public void Start(string remoteIp, int remotePort, Func<(float x, float y)> getTarget)
        {
            if (IsRunning) return;

            _client = new UdpClient();
            _client.Connect(remoteIp, remotePort);
            _cts = new CancellationTokenSource();
            _loop = SendLoopAsync(getTarget, _cts.Token);
        }

        private async Task SendLoopAsync(Func<(float x, float y)> getTarget, CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20)); // 50 Hz
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    var (x, y) = getTarget();
                    byte[] packet = BuildPacket(_seq, x, y);
                    await _client!.SendAsync(packet, ct);
                    PacketSent?.Invoke(_seq, x, y);
                    unchecked { _seq++; }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Error?.Invoke($"UDP 전송 오류: {ex.Message}");
                }
            }
        }

        private static byte[] BuildPacket(ushort seq, float x, float y)
        {
            Span<byte> buf = stackalloc byte[14];
            buf[0] = 0xBB;
            BinaryPrimitives.WriteUInt16LittleEndian(buf[1..3], seq);
            buf[3] = 0x10;
            BinaryPrimitives.WriteSingleLittleEndian(buf[4..8], x);
            BinaryPrimitives.WriteSingleLittleEndian(buf[8..12], y);
            BinaryPrimitives.WriteUInt16LittleEndian(buf[12..14], Crc16Ccitt(buf[..12]));
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

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            if (_loop != null)
            {
                try { await _loop.ConfigureAwait(false); }
                catch { }
            }
            _client?.Dispose();
            _client = null;
            _cts.Dispose();
            _cts = null;
        }

        public void Dispose()
        {
            _client?.Dispose();
            _cts?.Dispose();
        }
    }
}
