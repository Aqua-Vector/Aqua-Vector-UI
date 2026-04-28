using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AquaVectorUI.communication
{
    internal class EthernetCommunication : ICommunication
    {

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly string _ip;
        private readonly int _port;

        public event Action<string> OnDataReceived;

        public bool IsConnected
        {
            get
            {
                if (_client == null || !_client.Connected) return false;
                Socket s = _client.Client;
                // Poll이 true이고 읽을 데이터가 없으면 서버가 연결을 끊은 상태
                return !(s.Poll(0, SelectMode.SelectRead) && s.Available == 0);
            }
        }

        public EthernetCommunication(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public async Task ConnectAsync()
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_ip, _port);
            _stream = _client.GetStream();
        }

        public async Task DisconnectAsync()
        {
            _stream?.Close();
            _client?.Close();
            await Task.CompletedTask;
        }

        public async Task SendAsync(string data)
        {
            if (_stream == null || !IsConnected) return;
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            await _stream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
