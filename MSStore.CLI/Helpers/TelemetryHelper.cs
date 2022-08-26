// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;

namespace MSStore.CLI.Helpers
{
    internal static class TelemetryHelper
    {
        public static async Task<int> TrackCommandEventAsync(this TelemetryClient telemetryClient, string eventName, int returnCode, IDictionary<string, string>? properties = null, CancellationToken ct = default)
        {
            properties ??= new Dictionary<string, string>();

            properties.Add("ret", returnCode.ToString(CultureInfo.InvariantCulture));

            telemetryClient.TrackEvent(eventName, properties);
            if (!await telemetryClient.FlushAsync(ct))
            {
                Debugger.Break();
            }

            return returnCode;
        }

        public static Task<int> TrackCommandEventAsync(this TelemetryClient telemetryClient, string eventName, int returnCode, CancellationToken ct = default)
        {
            return TrackCommandEventAsync(telemetryClient, eventName, returnCode, null, ct);
        }

        public static Task<int> TrackCommandEventAsync<T>(this TelemetryClient telemetryClient, int returnCode, IDictionary<string, string>? properties = null, CancellationToken ct = default)
            where T : ICommandHandler
        {
            var typeName = typeof(T).FullName;
            if (typeName == null)
            {
                Debug.Assert(false, "typeName is null");
                return Task.FromResult(returnCode);
            }

            var prefix = "MSStore.CLI.Commands.";
            if (!typeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                Debug.Assert(false, $"Type {typeName} does not start with {prefix}");
                return Task.FromResult(returnCode);
            }

            typeName = typeName[prefix.Length..];

            var suffix = "Command+Handler";
            if (!typeName.EndsWith(suffix, StringComparison.Ordinal))
            {
                Debug.Assert(false, $"Type {typeName} does not end with {suffix}");
                return Task.FromResult(returnCode);
            }

            typeName = typeName.Remove(typeName.LastIndexOf(suffix, StringComparison.Ordinal));

            return TrackCommandEventAsync(telemetryClient, typeName, returnCode, properties, ct);
        }

        public static Task<int> TrackCommandEventAsync<T>(this TelemetryClient telemetryClient, int returnCode, CancellationToken ct = default)
            where T : ICommandHandler
        {
            return TrackCommandEventAsync<T>(telemetryClient, returnCode, null, ct);
        }

        public static Task<int> TrackCommandEventAsync<T>(this TelemetryClient telemetryClient, string productId, int returnCode, IDictionary<string, string>? properties = null, CancellationToken ct = default)
            where T : ICommandHandler
        {
            properties ??= new Dictionary<string, string>();

            properties.Add("ProductType", ((int)ProductTypeHelper.Solve(productId)).ToString(CultureInfo.InvariantCulture));

            return TrackCommandEventAsync<T>(telemetryClient, returnCode, properties, ct);
        }

        public static Task<int> TrackCommandEventAsync<T>(this TelemetryClient telemetryClient, string productId, int returnCode, CancellationToken ct = default)
            where T : ICommandHandler
        {
            return TrackCommandEventAsync<T>(telemetryClient, productId, returnCode, null, ct);
        }
    }
}
