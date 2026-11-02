// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MSStore.CLI.Services.Translation.Models;

namespace MSStore.CLI.Services.Translation
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization of
    /// Azure AI Translator calls.
    /// </summary>
    [JsonSerializable(typeof(List<TranslateInput>))]
    [JsonSerializable(typeof(List<TranslateResultItem>))]
    [JsonSerializable(typeof(TranslatorErrorResponse))]
    [JsonSerializable(typeof(TranslatorLanguagesResponse))]
    internal partial class TranslationSourceGenerationContext : JsonSerializerContext
    {
        private static TranslationSourceGenerationContext? _default;

        public static TranslationSourceGenerationContext GetCustom()
        {
            return _default ??= new TranslationSourceGenerationContext(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                PropertyNameCaseInsensitive = true,
                IgnoreReadOnlyFields = false,
                IgnoreReadOnlyProperties = false,
                IncludeFields = false
            });
        }
    }
}
