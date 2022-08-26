// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.Telemetry
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of Telemetry configurations.
    /// </summary>
    [JsonSerializable(typeof(TelemetryConnectionStringProvider))]
    [JsonSerializable(typeof(TelemetryConfigurations))]
    internal partial class TelemetrySourceGenerationContext : JsonSerializerContext
    {
    }
}
