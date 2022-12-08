// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.ElectronManager
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of the PWA Application Informations.
    /// </summary>
    [JsonSourceGenerationOptions]
    [JsonSerializable(typeof(ElectronManifest))]
    internal partial class ElectronManifestSourceGenerationContext : JsonSerializerContext
    {
        private static ElectronManifestSourceGenerationContext? _default;
        private static ElectronManifestSourceGenerationContext? _defaultPretty;

        public static ElectronManifestSourceGenerationContext GetCustom(bool writeIndented = false)
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

            static ElectronManifestSourceGenerationContext CreateCustom(bool writeIndented)
            {
                return new ElectronManifestSourceGenerationContext(new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNameCaseInsensitive = true,
                    IgnoreReadOnlyFields = false,
                    AllowTrailingCommas = true,
                    IgnoreReadOnlyProperties = false,
                    IncludeFields = false,
                    WriteIndented = writeIndented
                });
            }
        }
    }
}
