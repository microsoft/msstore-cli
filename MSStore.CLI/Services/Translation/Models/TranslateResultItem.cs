// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Services.Translation.Models
{
    /// <summary>
    /// A single element of the Translator v3.0 response body, which is a bare JSON array
    /// with one element per input, in the same order.
    /// </summary>
    internal class TranslateResultItem
    {
        /// <summary>
        /// Gets or sets the detected source language. Only present when the request omitted
        /// the <c>from</c> parameter, which is how automatic detection is requested.
        /// </summary>
        public DetectedLanguageInfo? DetectedLanguage { get; set; }

        public List<TranslationInfo>? Translations { get; set; }
    }
}
