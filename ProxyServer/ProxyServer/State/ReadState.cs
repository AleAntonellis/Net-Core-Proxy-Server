using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using ProxyServer.ProxyServer.Model;

namespace ProxyServer.ProxyServer.State
{
    internal class ReadState
    {
        public Socket Socket { get; }
        public byte[] Buffer { get; }
        public ProxyRequest Request { get; set; }

        public ReadState(Socket socket, byte[] buffer)
        {
            Socket = socket;
            Buffer = buffer;
        }
    }
}
