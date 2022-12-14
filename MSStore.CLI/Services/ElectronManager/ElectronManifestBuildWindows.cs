// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.ElectronManager
{
    internal class ElectronManifestBuildWindows
    {
        [JsonPropertyName("target")]
        public JsonNode? Targets { get; set; }
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}