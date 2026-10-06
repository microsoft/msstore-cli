// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.Translation
{
    /// <summary>
    /// Translates text into a target language.
    /// </summary>
    /// <remarks>
    /// The Microsoft Store APIs return no translated text and no language metadata for
    /// reviews, so translation has to come from a separate service. This interface keeps
    /// the provider and its wire format out of the commands.
    /// </remarks>
    internal interface ITranslationService
    {
        /// <summary>
        /// Resolves a user-supplied language code to the canonical code used by the service.
        /// </summary>
        /// <param name="language">The language code supplied by the user.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// The canonical language code, or <paramref name="language"/> unchanged when the
        /// supported-language list could not be retrieved.
        /// </returns>
        Task<string> ResolveLanguageAsync(string language, CancellationToken ct = default);

        /// <summary>
        /// Translates each entry of <paramref name="texts"/> into <paramref name="targetLanguage"/>.
        /// </summary>
        /// <param name="texts">The texts to translate.</param>
        /// <param name="targetLanguage">The canonical target language code.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// One entry per input, in the same order. Entries whose input was null or
        /// whitespace are null, and were never sent to the service.
        /// </returns>
        Task<IReadOnlyList<TranslationResult?>> TranslateAsync(IReadOnlyList<string?> texts, string targetLanguage, CancellationToken ct = default);
    }
}
