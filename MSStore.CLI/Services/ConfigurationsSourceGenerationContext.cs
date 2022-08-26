// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace MSStore.CLI.Services
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of the CLI Configurations.
    /// </summary>
    [JsonSerializable(typeof(Configurations))]
    internal partial class ConfigurationsSourceGenerationContext : JsonSerializerContext
    {
    }
}
