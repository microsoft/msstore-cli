// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services.Translation;

namespace MSStore.CLI.Commands.Reviews
{
    internal static class ReviewTranslator
    {
        /// <summary>
        /// Populates the translated fields of each review in place.
        /// </summary>
        /// <remarks>
        /// Titles and texts are sent in a single call so the service batches them together
        /// rather than paying the per-request overhead twice.
        /// </remarks>
        /// <param name="translationService">The translation service to use.</param>
        /// <param name="reviews">The reviews to translate, modified in place.</param>
        /// <param name="targetLanguage">The language to translate into.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task that completes once every review has been updated.</returns>
        public static async Task TranslateAsync(ITranslationService translationService, IReadOnlyList<AppReview> reviews, string targetLanguage, CancellationToken ct)
        {
            var language = await translationService.ResolveLanguageAsync(targetLanguage, ct);

            var texts = new List<string?>(reviews.Count * 2);
            foreach (var review in reviews)
            {
                texts.Add(review.ReviewTitle);
                texts.Add(review.ReviewText);
            }

            var translations = await translationService.TranslateAsync(texts, language, ct);

            for (var i = 0; i < reviews.Count; i++)
            {
                var title = translations[i * 2];
                var text = translations[(i * 2) + 1];

                reviews[i].TranslatedReviewTitle = title?.Text;
                reviews[i].TranslatedReviewText = text?.Text;

                // The body is the better signal for the review's language; fall back to the
                // title when the body was empty.
                reviews[i].DetectedLanguage = text?.DetectedLanguage ?? title?.DetectedLanguage;
            }
        }
    }
}
