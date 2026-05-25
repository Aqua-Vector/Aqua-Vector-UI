using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using AquaVectorUI.communication;

namespace AquaVectorUI.viewmodel
{
    public partial class MainViewModel
    {
        [RelayCommand]
        public async Task Connect()
        {
            try
            {
                if (Communication != null)
                {
                    Communication.OnDataReceived -= OnDataReceived;
                    await Communication.DisconnectAsync();
                }

                if (IsUartSelected)
                {
                    var uart = new UartCommunication(PortName, BaudRate);
                    uart.OnBinaryPacketReceived += DispatchUdpPacket;
                    uart.OnParseError += msg => AppendLog($"[UART 파싱 오류] {msg}");
                    Communication = uart;
                }
                else
                {
                    Communication = new EthernetCommunication(IpAddress, TcpPort);
                }

                Communication.OnDataReceived += OnDataReceived;
                await Communication.ConnectAsync();

                IsConnected = true;
                ConnectionStatusText = IsUartSelected
                    ? $"연결됨: {PortName}"
                    : $"연결됨: {IpAddress}:{TcpPort}";
                AppendLog($"✅ 연결 성공: {(IsUartSelected ? PortName : IpAddress)}");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatusText = "연결 실패";
                AppendLog($"❌ 연결 실패: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task Disconnect()
        {
            try
            {
                if (Communication != null)
                {
                    Communication.OnDataReceived -= OnDataReceived;
                    if (Communication is UartCommunication uart)
                    uart.OnBinaryPacketReceived -= DispatchUdpPacket;
                    await Communication.DisconnectAsync();
                    Communication = null;
                }

                IsConnected = false;
                ConnectionStatusText = "연결 안됨";
                AppendLog("🔌 연결 해제");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ 연결 해제 오류: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task Send()
        {
            if (IsConnected == false)
            {
                AppendLog("연결을 확인하세요");
                return;
            }
            else
            {
                AppendLog($"송신: {InputText}");
                if (string.IsNullOrWhiteSpace(InputText)) return;
                if (Communication == null || !IsConnected) return;
                await Communication.SendAsync(InputText + "\n");
                //AppendLog($"송신: {InputText}");
            }
        }
    }
}
