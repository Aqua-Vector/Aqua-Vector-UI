using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AquaVectorUI.communication
{
    public interface ICommunication
    {
        Task ConnectAsync();
        Task DisconnectAsync();
        Task SendAsync(string data);
        Task SendBytesAsync(byte[] data);
        event Action<string> OnDataReceived;
        bool IsConnected { get; }
    }
}
