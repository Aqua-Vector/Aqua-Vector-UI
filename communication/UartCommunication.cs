using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AquaVectorUI.communication
{
    public class UartCommunication : ICommunication
    {
        private SerialPort _port;
        private TaskCompletionSource<string> _receiveTcs;
        private StringBuilder _buffer = new StringBuilder();

        private readonly string _portName;
        private readonly int _baudRate;

        public event Action<string> OnDataReceived;

        public bool IsConnected => _port?.IsOpen ?? false;

        public UartCommunication(string portName, int baudRate)
        {
            _portName = portName;
            _baudRate = baudRate;
        }

        public Task ConnectAsync()
        {
            _port = new SerialPort(
                _portName,
                _baudRate,
                parity: Parity.None,
                dataBits: 8,
                stopBits: StopBits.One);
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
            {
                _port.WriteLine(data);
            }
            return Task.CompletedTask;

            //await _port.BaseStream.WriteAsync(data, 0, data.Length);
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string data = _port.ReadExisting();
            _buffer.Append(data); // 버퍼에 누적

            string bufferStr = _buffer.ToString();

            // 개행문자 기준으로 완성된 데이터만 출력
            if (bufferStr.Contains("\n"))
            {
                string[] lines = bufferStr.Split('\n');

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        OnDataReceived?.Invoke(line); // 데이터 오면 자동 발생
                        //Dispatcher.Invoke(() => AppendLog("📥 수신: " + line));
                    }
                }

                // 미완성 데이터는 버퍼에 보존
                _buffer.Clear();
                _buffer.Append(lines[lines.Length - 1]);
            }
        }
    }
}
