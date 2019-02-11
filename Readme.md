# Net Core Proxy Server

A proxy server in Net Core for HTTP and HTTPS traffic.

## How it works

In the class ProxyServer you could configure:

* the end point
* the port
* the limit

The other things you have to configure is the certificate for HTTPS traffic.

ProxyTunnel class, line 80:

```c#
var certificate = new X509Certificate2("<your certificate path>", "<your certificate password>");
```

