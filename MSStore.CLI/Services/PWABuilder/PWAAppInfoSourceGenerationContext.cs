// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.PWABuilder
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of the PWA Application Informations.
    /// </summary>
    [JsonSerializable(typeof(PWAAppInfo))]
    internal partial class PWAAppInfoSourceGenerationContext : JsonSerializerContext
    {
    }
}
