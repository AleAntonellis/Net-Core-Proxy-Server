using System;
using System.Runtime.CompilerServices;

namespace ProxyServer
{
    class Program
    {
        private static ProxyServer.ProxyServer server;

        static void Main(string[] args)
        {
            server = new ProxyServer.ProxyServer();
            server.Start();
            Console.ReadLine();
            server.Stop();
        }
    }
}
