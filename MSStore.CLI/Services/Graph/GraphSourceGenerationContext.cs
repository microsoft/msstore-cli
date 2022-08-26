// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.Graph
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of Microsoft Graph calls.
    /// </summary>
    [JsonSerializable(typeof(ListResponse<AzureApplication>))]
    [JsonSerializable(typeof(AppRegistrationRequest))]
    [JsonSerializable(typeof(AzureApplication))]
    [JsonSerializable(typeof(AppPasswordRegistrationRequest))]
    [JsonSerializable(typeof(CreateAppSecretResponse))]
    [JsonSerializable(typeof(AppUpdateRequest))]
    [JsonSerializable(typeof(CreatePrincipalRequest))]
    [JsonSerializable(typeof(CreatePrincipalResponse))]
    internal partial class GraphSourceGenerationContext : JsonSerializerContext
    {
        private static GraphSourceGenerationContext? _default;
        private static GraphSourceGenerationContext? _defaultPretty;

        public static GraphSourceGenerationContext GetCustom(bool writeIndented = false)
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

            static GraphSourceGenerationContext CreateCustom(bool writeIndented)
            {
                return new GraphSourceGenerationContext(new JsonSerializerOptions
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
