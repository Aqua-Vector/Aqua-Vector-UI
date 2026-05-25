using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AquaVectorUI.communication
{
    public class UdpLidarReceiver : IDisposable
    {
        private readonly int _port;
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;
        private Task? _receiveLoop;

        public event Action<byte[]>? PacketReceived;
        public event Action<string>? Error;

        public bool IsRunning => _cts is { IsCancellationRequested: false };

        public UdpLidarReceiver(int port)
        {
            _port = port;
        }

        // Returns the local IP address bound to the socket, or null on failure.
        public string? Start()
        {
            if (IsRunning) return null;

            _udpClient = new UdpClient(_port);
            _cts = new CancellationTokenSource();
            _receiveLoop = ReceiveLoopAsync(_cts.Token);

            return GetLocalIpAddress();
        }

        private static string? GetLocalIpAddress()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .FirstOrDefault();
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;

            _cts.Cancel();
            _udpClient?.Close(); // unblocks ReceiveAsync immediately

            if (_receiveLoop != null)
            {
                try { await _receiveLoop.ConfigureAwait(false); }
                catch { /* already handled inside the loop */ }
            }

            _cts.Dispose();
            _cts = null;
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient!.ReceiveAsync(ct);
                    PacketReceived?.Invoke(result.Buffer);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException)     { break; }
                catch (SocketException ex) when (ct.IsCancellationRequested ||
                                                 ex.SocketErrorCode == SocketError.Interrupted)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"UDP receive error: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _udpClient?.Dispose();
            _cts?.Dispose();
        }
    }
}
