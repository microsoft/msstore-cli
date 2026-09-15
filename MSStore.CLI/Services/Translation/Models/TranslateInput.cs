// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace MSStore.CLI.Services.Translation.Models
{
    /// <summary>
    /// A single element of the Translator v3.0 request body, which is a bare JSON array.
    /// </summary>
    internal class TranslateInput
    {
        /// <summary>
        /// Gets or sets the text to translate.
        /// </summary>
        /// <remarks>
        /// The wire name is pinned rather than left to the serializer's naming policy. The
        /// reference documentation is inconsistent: the request body section states the
        /// property is named <c>Text</c> and the curl examples and official C# quickstart
        /// send that, while the JSON sample in the same section and the published Swagger use
        /// <c>text</c>. The service accepts either, so this follows the normative prose and,
        /// more importantly, no longer changes if the shared naming policy is ever altered.
        /// </remarks>
        [JsonPropertyName("Text")]
        public string? Text { get; set; }
    }
}
