// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.Http
{
    /// <summary>
    /// Restricts requests whose URL comes from untrusted input (e.g. a PWA web manifest) to HTTPS endpoints
    /// on public IP addresses, so they cannot reach loopback, private network or cloud metadata services.
    /// </summary>
    internal static class PublicHttpsPolicy
    {
        public static bool TryCreateUri(string? url, [NotNullWhen(true)] out Uri? uri, [NotNullWhen(false)] out string? error)
        {
            uri = null;

            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                error = "URL is not a valid absolute URL.";
                return false;
            }

            if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                error = "Only https URLs are allowed.";
                return false;
            }

            if (!string.IsNullOrEmpty(parsed.UserInfo))
            {
                error = "URLs containing credentials are not allowed.";
                return false;
            }

            uri = parsed;
            error = null;
            return true;
        }

        /// <summary>
        /// Creates a handler that refuses direct connections to non-public addresses. The check runs at connect
        /// time on the address actually used, so it also covers redirects, IP literals and DNS rebinding.
        /// </summary>
        /// <returns>The configured handler.</returns>
        public static SocketsHttpHandler CreateHandler()
        {
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = ConnectToPublicAddressAsync,
            };
            handler.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.Online;
            return handler;
        }

        public static bool IsPublicAddress(IPAddress address)
        {
            ArgumentNullException.ThrowIfNull(address);

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (IPAddress.IsLoopback(address) ||
                address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();

                // Rejects 0/8, 10/8, 100.64/10 (CGNAT), 169.254/16 (link-local, cloud metadata),
                // 172.16/12, 192.168/16 and 224/3 (multicast, reserved, broadcast).
                return !(
                    b[0] == 0 ||
                    b[0] == 10 ||
                    (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||
                    (b[0] == 169 && b[1] == 254) ||
                    (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                    (b[0] == 192 && b[1] == 168) ||
                    b[0] >= 224);
            }

            return address.AddressFamily == AddressFamily.InterNetworkV6 &&
                !address.IsIPv6LinkLocal &&
                !address.IsIPv6SiteLocal &&
                !address.IsIPv6UniqueLocal &&
                !address.IsIPv6Multicast;
        }

        private static async ValueTask<System.IO.Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken ct)
        {
            var endPoint = context.DnsEndPoint;
            var host = endPoint.Host.TrimStart('[').TrimEnd(']');
            var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);

            // A configured proxy (usually on a private network) is allowed; every other connection must be public.
            if (!IsProxyConnection(context.InitialRequestMessage.RequestUri, host, endPoint.Port) &&
                (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a))))
            {
                throw new HttpRequestException($"Connection to '{host}' was blocked because it resolves to a non-public IP address.");
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, endPoint.Port, ct).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static bool IsProxyConnection(Uri? requestUri, string host, int port)
        {
            if (requestUri == null || HttpClient.DefaultProxy.IsBypassed(requestUri))
            {
                return false;
            }

            var proxyUri = HttpClient.DefaultProxy.GetProxy(requestUri);
            return proxyUri != null &&
                !string.Equals(proxyUri.IdnHost, requestUri.IdnHost, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(proxyUri.IdnHost, host, StringComparison.OrdinalIgnoreCase) &&
                proxyUri.Port == port;
        }
    }
}
