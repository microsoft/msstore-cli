// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using MSStore.CLI.Commands.Reviews;

namespace MSStore.CLI.Commands
{
    internal class ReviewsCommand : Command
    {
        /// <summary>
        /// The language used when --translate is passed without a value.
        /// </summary>
        internal const string DefaultTranslateLanguage = "en";

        internal static readonly Argument<string> ProductIdArgument;
        internal static readonly Argument<string> ReviewIdArgument;
        internal static readonly Option<DateOnly?> StartDateOption;
        internal static readonly Option<DateOnly?> EndDateOption;
        internal static readonly Option<string> TranslateOption;

        static ReviewsCommand()
        {
            ProductIdArgument = new Argument<string>("productId")
            {
                Description = "The product ID."
            };

            ReviewIdArgument = new Argument<string>("reviewId")
            {
                Description = "The review ID."
            };

            StartDateOption = new Option<DateOnly?>("--startDate")
            {
                Description = "Only return reviews submitted on or after this date (yyyy-MM-dd). If omitted, reviews from all dates are returned."
            };

            EndDateOption = new Option<DateOnly?>("--endDate")
            {
                Description = "Only return reviews submitted on or before this date (yyyy-MM-dd). If omitted, reviews from all dates are returned."
            };

            TranslateOption = new Option<string>("--translate")
            {
                Description = $"Translate the review title and text into this language, using Azure AI Translator. Defaults to '{DefaultTranslateLanguage}' when no language is given. The Microsoft Store provides no translation of its own, so this requires your own Translator key.",
                Arity = ArgumentArity.ZeroOrOne
            };
        }

        public ReviewsCommand(ListCommand listCommand, GetCommand getCommand)
            : base("reviews", "Execute reviews related tasks.")
        {
            Subcommands.Add(listCommand);
            Subcommands.Add(getCommand);
        }
    }
}
