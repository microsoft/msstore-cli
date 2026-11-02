// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.CLI.Services.Translation.Models
{
    /// <summary>
    /// The response of <c>GET /languages?api-version=3.0&amp;scope=translation</c>, which
    /// requires no authentication.
    /// </summary>
    internal class TranslatorLanguagesResponse
    {
        /// <summary>
        /// Gets or sets the supported languages, keyed by BCP-47 tag.
        /// </summary>
        public Dictionary<string, TranslatorLanguage>? Translation { get; set; }
    }
}
