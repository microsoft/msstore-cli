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
        // IANA IPv4 special-purpose registry entries that are not globally reachable, plus multicast and reserved.
        private static readonly IPNetwork[] BlockedIPv4Networks =
        [
            IPNetwork.Parse("0.0.0.0/8"),
            IPNetwork.Parse("10.0.0.0/8"),
            IPNetwork.Parse("100.64.0.0/10"),
            IPNetwork.Parse("127.0.0.0/8"),
            IPNetwork.Parse("169.254.0.0/16"),
            IPNetwork.Parse("172.16.0.0/12"),
            IPNetwork.Parse("192.0.0.0/24"),
            IPNetwork.Parse("192.0.2.0/24"),
            IPNetwork.Parse("192.88.99.0/24"),
            IPNetwork.Parse("192.168.0.0/16"),
            IPNetwork.Parse("198.18.0.0/15"),
            IPNetwork.Parse("198.51.100.0/24"),
            IPNetwork.Parse("203.0.113.0/24"),
            IPNetwork.Parse("224.0.0.0/3"),
        ];

        // Only global unicast IPv6 is allowed, minus the special-purpose ranges carved out of it
        // (IETF protocol assignments incl. Teredo and benchmarking, documentation, 6to4).
        private static readonly IPNetwork GlobalUnicastIPv6Network = IPNetwork.Parse("2000::/3");

        private static readonly IPNetwork[] BlockedIPv6Networks =
        [
            IPNetwork.Parse("2001::/23"),
            IPNetwork.Parse("2001:db8::/32"),
            IPNetwork.Parse("2002::/16"),
            IPNetwork.Parse("3fff::/20"),
        ];

        // Well-known NAT64 prefix (RFC 6052): judged by the embedded IPv4 address.
        private static readonly IPNetwork Nat64IPv6Network = IPNetwork.Parse("64:ff9b::/96");
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

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return !BlockedIPv4Networks.Any(n => n.Contains(address));
            }

            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return false;
            }

            if (Nat64IPv6Network.Contains(address))
            {
                return IsPublicAddress(new IPAddress(address.GetAddressBytes()[12..16]));
            }

            return GlobalUnicastIPv6Network.Contains(address) && !BlockedIPv6Networks.Any(n => n.Contains(address));
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
