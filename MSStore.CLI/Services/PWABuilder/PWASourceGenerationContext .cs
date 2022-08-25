// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.PWABuilder
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of PWA Builder API calls.
    /// </summary>
    [JsonSourceGenerationOptions]
    [JsonSerializable(typeof(JsonDocument))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(GenerateZipRequest))]
    [JsonSerializable(typeof(WebManifestFetchResponse))]
    internal partial class PWASourceGenerationContext : JsonSerializerContext
    {
        public static PWASourceGenerationContext GetCustom(bool writeIndented = false)
        {
            return new PWASourceGenerationContext(new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                PropertyNameCaseInsensitive = true,
                IgnoreReadOnlyFields = false,
                IgnoreReadOnlyProperties = false,
                IncludeFields = false,
                WriteIndented = writeIndented,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            });
        }
    }
}
