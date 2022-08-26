// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MSStore.CLI.Services.Telemetry
{
    internal class TelemetryConnectionStringProvider
    {
        public string? AIConnectionString { get; set; }

        internal static async Task<TelemetryConnectionStringProvider?> LoadAsync(ILogger? logger = null, CancellationToken ct = default)
        {
            var assembly = typeof(TelemetryConnectionStringProvider).GetTypeInfo().Assembly;
            using var resource = assembly.GetManifestResourceStream("MSStore.CLI.config.json");

            if (resource == null)
            {
                return null;
            }

            try
            {
                return await JsonSerializer.DeserializeAsync(resource, TelemetrySourceGenerationContext.Default.TelemetryConnectionStringProvider, ct);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error while reading telemetry configuration.");
                return null;
            }
        }
    }
}
