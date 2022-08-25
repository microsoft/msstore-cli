// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.Graph
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of Microsoft Graph calls.
    /// </summary>
    [JsonSerializable(typeof(ListResponse<Organization>))]
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
        public static GraphSourceGenerationContext GetCustom(bool writeIndented = false)
        {
            return new GraphSourceGenerationContext(new JsonSerializerOptions
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
