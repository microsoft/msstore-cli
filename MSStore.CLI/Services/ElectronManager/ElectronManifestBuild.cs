// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.ElectronManager
{
    internal class ElectronManifestBuild
    {
        [JsonPropertyName("appId")]
        public string? AppId { get; set; }
        [JsonPropertyName("productId")]
        public string? ProductId { get; set; }
        [JsonPropertyName("win")]
        public ElectronManifestBuildWindows? Windows { get; set; }
        [JsonPropertyName("appx")]
        public ElectronManifestBuildAppX? Appx { get; set; }
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}