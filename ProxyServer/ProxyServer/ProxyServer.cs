using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ProxyServer.ProxyServer.Model;
using ProxyServer.ProxyServer.State;

namespace ProxyServer.ProxyServer
{
    internal class ProxyServer : IDisposable
    {
        private Socket server;
        private List<Socket> serverSockets;
        private string ipEndPoint = "127.0.0.1";
        private int port = 8080;
        private int limit = 200;
        private ProxyState state;

        public ProxyServer()
        {
            state = ProxyState.None;
            serverSockets = new List<Socket>();
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var endPoint = new IPEndPoint(IPAddress.Parse(ipEndPoint), port);
            server.Bind(endPoint);
            server.Listen(limit);
            server.BeginAccept(AcceptClient, null);
            state = ProxyState.Running;
            Console.WriteLine("Proxy Started");
        }

        public void Stop()
        {
            KillAllPendingSockets();
            if (server.Connected) server.Shutdown(SocketShutdown.Both);
            server.Close();
            server.Dispose();
            state = ProxyState.Stopped;
            Console.WriteLine("Proxy Stopped");
            Console.ReadLine();
        }

        private void AcceptClient(IAsyncResult ar)
        {
            try
            {
                Socket client = server.EndAccept(ar);
                serverSockets.Add(client);
                var readState = new ReadState(client, new byte[2048]);
                client.BeginReceive(readState.Buffer, 0, readState.Buffer.Length, SocketFlags.None, ReadPackets, readState);
            }
            catch (Exception e)
            {
                // ignored
            }
            finally
            {
                if (state == ProxyState.Running) server.BeginAccept(AcceptClient, null);
            }
        }

        private void ReadPackets(IAsyncResult ar)
        {
            var readState = (ReadState)ar.AsyncState;
            var socket = readState.Socket;
            var buffer = readState.Buffer;

            int read = -1;

            try
            {
                read = socket.EndReceive(ar);
            }
            catch (Exception)
            {
                Console.WriteLine("[DISCONNECT] Client Disconnected from server");
                KillSocket(socket);
                return;
            }

            if (read == 0)
            {
                if (state == ProxyState.Running) socket.BeginReceive(readState.Buffer, 0, readState.Buffer.Length, SocketFlags.None, ReadPackets, readState);
                return;
            }

            var text = Encoding.ASCII.GetString(buffer, 0, read);

            if (readState.Request != null && !readState.Request.Ended)
            {
                text = readState.Request.FullRequest + text;
            }

            var request = new ProxyRequest(text);
            if (request.IsFake)
            {
                KillSocket(socket);
                return;
            }

            if (request.Ended && !request.IsFake)
            {
                Console.WriteLine(request.ToString());
                var proxyTunnel = new ProxyTunnel(socket, request);
                proxyTunnel.CreateTunnel();
                proxyTunnel.Send();
                return;
            }

            if (request.Ended) return;
            readState.Request = request;
            Array.Clear(buffer, 0, buffer.Length);
            if (state == ProxyState.Running) socket.BeginReceive(readState.Buffer, 0, readState.Buffer.Length, SocketFlags.None, ReadPackets, readState);
        }

        private void KillAllPendingSockets()
        {
            var listToKill = new List<Socket>();
            serverSockets.ForEach(x => listToKill.Add(x));
            listToKill.ForEach(KillSocket);
        }

        private void KillSocket(Socket socket)
        {
            if (serverSockets.Any(x => x.Equals(socket)))
                serverSockets.Remove(socket);

            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Disconnect(false);
            }
            catch (Exception e)
            {
                //ignore
            }
            finally
            {
                socket.Close();
                socket.Dispose();
            }
        }
    }

    internal enum ProxyState
    {
        None,
        Running,
        Stopped
    }
}
