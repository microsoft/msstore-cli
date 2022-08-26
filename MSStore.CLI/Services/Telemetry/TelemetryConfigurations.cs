// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.CLI.Services.Telemetry
{
    internal class TelemetryConfigurations
    {
        public bool? TelemetryEnabled { get; set; }
        public string? TelemetryGuid { get; set; }
        public DateTime? TelemetryGuidDateTime { get; set; }
    }
}
