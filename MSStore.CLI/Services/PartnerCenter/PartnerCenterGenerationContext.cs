// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.PartnerCenter
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of Microsoft Partner Center calls.
    /// </summary>
    [JsonSourceGenerationOptions]
    [JsonSerializable(typeof(AccountEnrollments))]
    internal partial class PartnerCenterGenerationContext : JsonSerializerContext
    {
        private static PartnerCenterGenerationContext? _default;
        private static PartnerCenterGenerationContext? _defaultPretty;

        public static PartnerCenterGenerationContext GetCustom(bool writeIndented = false)
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

            static PartnerCenterGenerationContext CreateCustom(bool writeIndented)
            {
                return new PartnerCenterGenerationContext(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
