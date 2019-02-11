using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProxyServer.ProxyServer.Model
{
    internal class ProxyRequest
    {
        public string FullRequest { get; }
        public bool Ended => FullRequest.EndsWith("\r\n\r\n");
        public bool IsFake => string.IsNullOrEmpty(FullRequest) || !Version.Contains("HTTP");
        public string Method { get; private set; }
        public string Target { get; private set; }
        public string Version { get; private set; }
        public Dictionary<string, string> Headers { get; private set; }
        public string HtmlBody { get; private set; }

        public ProxyRequest(string request)
        {
            FullRequest = request;
            var comparer = StringComparer.OrdinalIgnoreCase;
            Headers = new Dictionary<string, string>(comparer);
            Serialize();
        }
        
        public override string ToString()
        {
            return $"[REQUEST: {Method}] {Target}";
        }

        public void AddHostAndHttpsProtocolToTarget(string host)
        {
            this.Target = $"https://{host}{this.Target}";
        }

        private void Serialize()
        {
            if (string.IsNullOrEmpty(FullRequest)) return;

            var requestLines = FullRequest.Split('\n').ToList();

            ExtractMainInfo(requestLines);

            var isBody = false;
            foreach (var line in requestLines.GetRange(1, requestLines.Count - 1))
            {
                var cleanline = line.Replace("\r", string.Empty);
                if (string.IsNullOrEmpty(cleanline))
                {
                    isBody = true;
                    continue;
                }

                if (isBody)
                {
                    HtmlBody += cleanline;
                    if (requestLines.IndexOf(line).Equals(cleanline.Length - 1))
                        HtmlBody += Environment.NewLine;
                }
                else
                {
                    AddHeader(cleanline);
                }
            }
        }

        private void ExtractMainInfo(IReadOnlyList<string> requestLines)
        {
            var infoLine = requestLines[0].Replace("\r", string.Empty);
            var iParts = infoLine.Split(' ');
            Method = iParts[0];
            Target = iParts[1];
            Version = iParts[2];
        }

        private void AddHeader(string line)
        {
            var hName = line.Substring(0, line.IndexOf(':'));
            var hValue = line.Substring(line.IndexOf(':') + 2, line.Length - line.IndexOf(':') - 2);
            Headers.Add(hName, hValue);
        }
    }
}
