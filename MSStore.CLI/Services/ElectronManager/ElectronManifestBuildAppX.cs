// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.ElectronManager
{
    internal class ElectronManifestBuildAppX
    {
        [JsonPropertyName("applicationId")]
        public string? ApplicationId { get; set; }
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
        [JsonPropertyName("publisher")]
        public string? Publisher { get; set; }
        [JsonPropertyName("publisherDisplayName")]
        public string? PublisherDisplayName { get; set; }
        [JsonPropertyName("identityName")]
        public string? IdentityName { get; set; }
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}