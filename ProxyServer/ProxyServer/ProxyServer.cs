using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ProxyServer.ProxyServer.Interface;
using ProxyServer.ProxyServer.Model;
using ProxyServer.ProxyServer.State;

namespace ProxyServer.ProxyServer
{
    public class ProxyServer : IProxyServer, IDisposable
    {
        private Socket server;
        private readonly List<Socket> serverSockets;
        private string ipEndPoint;
        private int port;
        private int limit;
        private ProxyState state;

        public ProxyServer()
        {
            state = ProxyState.None;
            serverSockets = new List<Socket>();
            ipEndPoint = "127.0.0.1";
            port = 8080;
            limit = 200;
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

        public string Status()
        {
            return state.ToString();
        }

        public void SetPort(int portNumber)
        {
            if (state != ProxyState.Running)
            {
                port = portNumber;
            }
            else
            {
                throw new Exception("Proxy is running");
            }
        }

        public void SetEndPoint(string endPoint)
        {
            if (state != ProxyState.Running)
            {
                ipEndPoint = endPoint;
            }
            else
            {
                throw new Exception("Proxy is running");
            }
        }

        public void SetLimit(int connectionLimit)
        {
            if (state != ProxyState.Running)
            {
                limit = connectionLimit;
            }
            else
            {
                throw new Exception("Proxy is running");
            }
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
            catch (Exception)
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

            var read = -1;

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
                InternalSend(socket, request);
                return;
            }

            if (request.Ended) return;
            readState.Request = request;
            Array.Clear(buffer, 0, buffer.Length);
            if (state == ProxyState.Running) socket.BeginReceive(readState.Buffer, 0, readState.Buffer.Length, SocketFlags.None, ReadPackets, readState);
        }

        private static void InternalSend(Socket socket, ProxyRequest request)
        {
            var proxyTunnel = new ProxyTunnel(socket, request);
            proxyTunnel.CreateTunnel();
            proxyTunnel.Send();
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
            catch (Exception)
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
