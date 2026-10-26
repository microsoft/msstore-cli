// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using MSStore.CLI.Commands;

namespace MSStore.CLI.Helpers
{
    internal static class ParseResultExtensions
    {
        public static bool IsVerbose(this ParseResult parseResult)
        {
            return parseResult.RootCommandResult.Command is MicrosoftStoreCLI storeCLI &&
                    parseResult.GetValue(MicrosoftStoreCLI.VerboseOption);
        }

        /// <summary>
        /// Resolves the language requested through --translate, or null when the option was
        /// not supplied. Passing the option without a value selects the default language.
        /// </summary>
        /// <param name="parseResult">The parsed command line.</param>
        /// <returns>The requested language, or null when --translate was not supplied.</returns>
        public static string? GetTranslateLanguage(this ParseResult parseResult)
        {
            if (parseResult.GetResult(ReviewsCommand.TranslateOption) == null)
            {
                return null;
            }

            var language = parseResult.GetValue(ReviewsCommand.TranslateOption);

            return string.IsNullOrWhiteSpace(language) ? ReviewsCommand.DefaultTranslateLanguage : language;
        }

        /// <summary>
        /// Indicates whether the caller narrowed the reviews query by date.
        /// </summary>
        /// <param name="parseResult">The parsed command line.</param>
        /// <returns>True when --startDate or --endDate was supplied.</returns>
        /// <remarks>
        /// Leaving both options off sends no date parameters at all, and the service then
        /// returns reviews from every date, so messages must not imply a date range was
        /// applied in that case.
        /// </remarks>
        public static bool NarrowedReviewsByDate(this ParseResult parseResult)
        {
            return parseResult.GetResult(ReviewsCommand.StartDateOption) != null
                || parseResult.GetResult(ReviewsCommand.EndDateOption) != null;
        }
    }
}
