// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services.Translation
{
    /// <summary>
    /// The translation of a single piece of text.
    /// </summary>
    internal class TranslationResult(string text, string? detectedLanguage)
    {
        /// <summary>
        /// Gets the translated text.
        /// </summary>
        public string Text { get; } = text;

        /// <summary>
        /// Gets the language detected in the source text, or null when the service did not
        /// report one.
        /// </summary>
        public string? DetectedLanguage { get; } = detectedLanguage;
    }
}
