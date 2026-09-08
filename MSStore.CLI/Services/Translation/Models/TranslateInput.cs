// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services.Translation.Models
{
    /// <summary>
    /// A single element of the Translator v3.0 request body, which is a bare JSON array.
    /// </summary>
    internal class TranslateInput
    {
        public string? Text { get; set; }
    }
}
