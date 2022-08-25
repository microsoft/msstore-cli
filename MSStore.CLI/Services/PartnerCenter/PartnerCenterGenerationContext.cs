// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.PartnerCenter
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of Microsoft Partner Center calls.
    /// </summary>
    [JsonSerializable(typeof(AccountEnrollments))]
    internal partial class PartnerCenterGenerationContext : JsonSerializerContext
    {
        public static PartnerCenterGenerationContext GetCustom(bool writeIndented = false)
        {
            return new PartnerCenterGenerationContext(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
