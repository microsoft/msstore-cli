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
        private static PWASourceGenerationContext? _default;
        private static PWASourceGenerationContext? _defaultPretty;

        public static PWASourceGenerationContext GetCustom(bool writeIndented = false)
        {
            if (writeIndented)
            {
                return _defaultPretty ??=
                    CreateCustom(writeIndented);
            }
            else
            {
                return _default ??=
                    CreateCustom(writeIndented);
            }

            static PWASourceGenerationContext CreateCustom(bool writeIndented)
            {
                return new PWASourceGenerationContext(new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    PropertyNameCaseInsensitive = true,
                    IgnoreReadOnlyFields = false,
                    IgnoreReadOnlyProperties = false,
                    IncludeFields = false,
                    WriteIndented = writeIndented
                });
            }
        }
    }
}
