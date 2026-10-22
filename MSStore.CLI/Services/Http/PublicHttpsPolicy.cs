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
        /// Creates a handler that refuses connections to non-public addresses. The check runs at connect
        /// time on the address actually used, so it also covers redirects, IP literals and DNS rebinding.
        /// Proxies are disabled: through a proxy the connection goes to the proxy, which would then reach the
        /// destination without it ever being checked.
        /// </summary>
        /// <returns>The configured handler.</returns>
        public static SocketsHttpHandler CreateHandler()
        {
            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
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
                // 172.16/12, 192.168/16, 198.18/15 (benchmarking) and 224/3 (multicast, reserved, broadcast).
                return !(
                    b[0] == 0 ||
                    b[0] == 10 ||
                    (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||
                    (b[0] == 169 && b[1] == 254) ||
                    (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                    (b[0] == 192 && b[1] == 168) ||
                    (b[0] == 198 && (b[1] == 18 || b[1] == 19)) ||
                    b[0] >= 224);
            }

            if (address.AddressFamily != AddressFamily.InterNetworkV6 ||
                address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal ||
                address.IsIPv6UniqueLocal ||
                address.IsIPv6Multicast)
            {
                return false;
            }

            var v6 = address.GetAddressBytes();

            // 64:ff9b:1::/48 is the local-use NAT64 prefix (RFC 8215) and can translate to any IPv4 address.
            if (v6[0] == 0x00 && v6[1] == 0x64 && v6[2] == 0xff && v6[3] == 0x9b && v6[4] == 0x00 && v6[5] == 0x01)
            {
                return false;
            }

            // 64:ff9b::/96 is the well-known NAT64 prefix (RFC 6052); judge it by the embedded IPv4 address.
            if (v6[0] == 0x00 && v6[1] == 0x64 && v6[2] == 0xff && v6[3] == 0x9b && v6[4..12].All(b => b == 0))
            {
                return IsPublicAddress(new IPAddress(v6[12..16]));
            }

            return true;
        }

        private static async ValueTask<System.IO.Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken ct)
        {
            var endPoint = context.DnsEndPoint;
            var host = endPoint.Host.TrimStart('[').TrimEnd(']');
            var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);

            if (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a)))
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
    }
}
