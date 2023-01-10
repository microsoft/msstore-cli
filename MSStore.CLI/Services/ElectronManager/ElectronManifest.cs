// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.ElectronManager
{
    internal class ElectronManifest
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        [JsonPropertyName("build")]
        public ElectronManifestBuild? Build { get; set; }
        [JsonPropertyName("msstoreCliAppId")]
        [JsonPropertyOrder(int.MaxValue)]
        public string? MSStoreCLIAppID { get; set; }
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
}
