using CommunityToolkit.Mvvm.Input;
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

                Communication = IsUartSelected
                    ? (ICommunication)new UartCommunication(PortName, BaudRate)
                    : new EthernetCommunication(IpAddress, TcpPort);

                Communication.OnDataReceived += OnDataReceived;
                await Communication.ConnectAsync();

                IsConnected = true;
                ConnectionStatusText = IsUartSelected
                    ? $"연결됨: {PortName}"
                    : $"연결됨: {IpAddress}:{TcpPort}";
                AppendLog($"✅ 연결 성공: {(IsUartSelected ? PortName : IpAddress)}");
            }
            catch (System.Exception ex)
            {
                IsConnected = false;
                ConnectionStatusText = "연결 실패";
                AppendLog($"❌ 연결 실패: {ex.Message}");
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
