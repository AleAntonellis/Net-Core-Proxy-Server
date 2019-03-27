using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ProxyServer.ProxyServer.Model
{
    internal class ProxyTunnel
    {
        private readonly Socket socket;
        private ProxyRequest request;
        private string host;

        public Protocol Protocol { get; private set; }

        public ProxyTunnel(Socket socket, ProxyRequest request)
        {
            this.socket = socket;
            this.request = request;
        }

        public void CreateTunnel()
        {
            var requestHost = request.Headers["Host"];
            if (request.Method.Equals("CONNECT"))
            {
                host = requestHost.Replace(":443", string.Empty);
                Protocol = Protocol.Https;
            }
            else
            {
                Protocol = Protocol.Http;
                host = requestHost;
            }
        }

        public void Send()
        {
            switch (Protocol)
            {
                case Protocol.None:
                    throw new NotImplementedException();
                case Protocol.Http:
                    SendHttp();
                    break;
                case Protocol.Https:
                    InitHttps();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void SendHttp()
        {
            if (host == null) { Generate404(); return; }
            var response = GetHttpWebResponse(out var resp);
            Generate200(response.Headers, resp);
        }
        
        private void InitHttps()
        {
            try
            {
                Stream clientStream = new NetworkStream(socket, false);
            
                StreamWriter connectStreamWriter = new StreamWriter(clientStream);
                connectStreamWriter.WriteLine("HTTP/1.0 200 Connection established");
                connectStreamWriter.WriteLine($"Timestamp: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}");
                connectStreamWriter.WriteLine("Proxy-agent: matt-dot-net");
                connectStreamWriter.WriteLine();
                connectStreamWriter.Flush();

                var certificate = new X509Certificate2("<your certificate path>", "<your certificate password>");
            
                var sslStream = new SslStream(clientStream, false);
                sslStream.AuthenticateAsServer(certificate, false, enabledSslProtocols: SslProtocols.Tls, checkCertificateRevocation: true);
            
                var clientStreamReader = new StreamReader(sslStream);

                string clientRequest = clientStreamReader.ReadToEnd();
            
                request = new ProxyRequest(clientRequest);
                request.AddHostAndHttpsProtocolToTarget(host);
                var response = GetHttpWebResponse(out var resp);
                var responseToFlush = Get200RawText(response.Headers, resp);

                StreamWriter responseStreamReader = new StreamWriter(sslStream);
                responseStreamReader.Write(responseToFlush);
                responseStreamReader.Flush();

                Console.WriteLine("Send HTTPS response");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Disconnect(false);
            }
        }
        
        private void Generate404()
        {
            try
            {
                string text = "HTTP/1.1 404 Not Found\r\nTimestamp: " + DateTime.Now + "\r\nProxy-Agent: ah101\r\n\r\n";
                StreamWriter responseStreamReader = new StreamWriter(new NetworkStream(socket, false));
                responseStreamReader.Write(text);
                responseStreamReader.Flush();
                Console.WriteLine("Send HTTP 404 Response");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Disconnect(false);
            }
        }

        private void Generate200(WebHeaderCollection headers, string response)
        {
            try
            {
                var text = Get200RawText(headers, response);
                StreamWriter responseStreamReader = new StreamWriter(new NetworkStream(socket, false));
                responseStreamReader.Write(text);
                responseStreamReader.Flush();
                Console.WriteLine("Send HTTP 200 Response");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Disconnect(false);
            }
        }

        private static string Get200RawText(WebHeaderCollection headers, string response)
        {
            string text = "HTTP/1.1 200 OK\r\n";
            headers.AllKeys.ToList().ForEach(k => text += $"{k}: {headers[k]}\r\n");
            text += $"\r\n{response}\r\n\r\n";
            return text;
        }
        
        private HttpWebResponse GetHttpWebResponse(out string resp)
        {
            var webRequest = WebRequest.Create(request.Target);
            webRequest.Method = request.Method;
            request.Headers.Keys.ToList().ForEach(k => webRequest.Headers.Add(k, request.Headers[k]));

            HttpWebRequest httpRequest = (HttpWebRequest) webRequest;
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            if (!string.IsNullOrEmpty(request.HtmlBody))
            {
                var data = Encoding.ASCII.GetBytes(request.HtmlBody);
                httpRequest.ContentLength = data.Length;
                using (var bodyStream = httpRequest.GetRequestStream())
                {
                    bodyStream.Write(data, 0, data.Length);
                }
            }

            HttpWebResponse response = (HttpWebResponse) httpRequest.GetResponse();
            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream);
            resp = reader.ReadToEnd();
            return response;
        }

    }

    internal enum Protocol
    {
        None,
        Http,
        Https
    }
}

