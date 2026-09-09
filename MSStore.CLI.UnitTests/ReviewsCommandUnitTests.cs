// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.Translation;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ReviewsCommandUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
            AddFakeApps();
            AddFakeReviews();
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldReturnZero()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA"
                ]);

            // The table is human-facing output, so it goes to the injected console (stderr).
            result.Error.Should().Contain("FakeReviewer1");
            result.Error.Should().Contain("Great app");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldRenderReviewsMissingOptionalFields()
        {
            // The analytics API omits fields entirely rather than returning them as null,
            // so a sparse review must not break rendering.
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA"
                ]);

            result.Error.Should().Contain("Um jogo fantástico");
            result.Error.Should().Contain("*---- (1)");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldFilterByMarket()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--market",
                    "BR"
                ]);

            result.Error.Should().Contain("Um jogo fantástico");
            result.Error.Should().NotContain("Great app");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldFilterByRating()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--rating",
                    "5"
                ]);

            result.Error.Should().Contain("Great app");
            result.Error.Should().NotContain("Um jogo fantástico");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldRejectInvalidRating()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--rating",
                    "9"
                ],
                -1);

            result.Error.Should().Contain("--rating must be between 1 and 5.");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldRejectTooLargeTop()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--top",
                    "10001"
                ],
                -1);

            result.Error.Should().Contain("--top cannot be greater than 10000.");
        }

        [TestMethod]
        [DataRow("0")]
        [DataRow("-5")]
        public async Task ReviewsListCommandShouldRejectNonPositiveTop(string top)
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--top",
                    top
                ],
                -1);

            result.Error.Should().Contain("--top must be at least 1.");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldRejectNegativeSkip()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--skip",
                    "-3"
                ],
                -1);

            result.Error.Should().Contain("--skip cannot be negative.");
        }

        [TestMethod]
        public async Task ReviewsListCommandIsNotSupportedForUnpackagedApps()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    Guid.Empty.ToString()
                ],
                -1);

            result.Error.Should().Contain("This command is not supported for unpackaged applications.");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldReportWhenThereAreNoReviews()
        {
            FakeReviews.Clear();

            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA"
                ]);

            result.Error.Should().Contain("This application has no reviews.");

            // No date or filter option was passed, so the service returns reviews from every
            // date. Referring to a period or filters here would misdirect the user.
            result.Error.Should().NotContain("period");
            result.Error.Should().NotContain("filters");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldMentionThePeriodWhenNarrowedByDate()
        {
            FakeReviews.Clear();

            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--startDate",
                    "2024-01-01"
                ]);

            result.Error.Should().Contain("for the requested period.");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldMentionFiltersWhenNarrowedByFilter()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--market",
                    "ZZ"
                ]);

            result.Error.Should().Contain("matching the requested filters.");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldMentionBothWhenNarrowedByDateAndFilter()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--startDate",
                    "2024-01-01",
                    "--rating",
                    "2"
                ]);

            result.Error.Should().Contain("matching the requested period and filters.");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldTranslateWhenRequested()
        {
            AddFakeTranslations();

            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--translate"
                ]);

            result.Error.Should().Contain("[en] Great app");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldTranslateIntoTheRequestedLanguage()
        {
            AddFakeTranslations();

            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--translate",
                    "pt"
                ]);

            result.Error.Should().Contain("[pt] Great app");
        }

        [TestMethod]
        public async Task ReviewsListCommandShouldReportMissingTranslatorKey()
        {
            FakeTranslationService
                .Setup(x => x.TranslateAsync(It.IsAny<IReadOnlyList<string?>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TranslationException("No Azure AI Translator key is configured."));

            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "list",
                    "9PN3ABCDEFGA",
                    "--translate"
                ],
                -1);

            result.Error.Should().Contain("No Azure AI Translator key is configured.");
        }

        [TestMethod]
        public async Task ReviewsGetCommandShouldReturnJsonForKnownReview()
        {
            var reviewId = FakeReviews[0].Id!;

            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "get",
                    "9PN3ABCDEFGA",
                    reviewId
                ]);

            result.Output.Should().Contain($"\"Id\": \"{reviewId}\"");
            result.Output.Should().Contain("\"ReviewTitle\": \"Great app\"");
        }

        [TestMethod]
        public async Task ReviewsGetCommandShouldNotEmitTranslatedFieldsWhenNotTranslating()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "get",
                    "9PN3ABCDEFGA",
                    FakeReviews[0].Id!
                ]);

            result.Output.Should().NotContain("TranslatedReviewTitle");
            result.Output.Should().NotContain("DetectedLanguage");
        }

        [TestMethod]
        public async Task ReviewsGetCommandShouldEmitTranslatedFieldsWhenTranslating()
        {
            AddFakeTranslations();

            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "get",
                    "9PN3ABCDEFGA",
                    FakeReviews[1].Id!,
                    "--translate"
                ]);

            // System.Text.Json escapes non-ASCII by default, so the accented characters are
            // \u-escaped in the payload. That is valid JSON and decodes back correctly; the
            // same encoder is used by every other command that emits JSON.
            result.Output.Should().Contain("\"TranslatedReviewTitle\": \"[en] Um jogo fant\\u00E1stico\"");
            result.Output.Should().Contain("\"DetectedLanguage\": \"pt\"");
        }

        [TestMethod]
        public async Task ReviewsGetCommandShouldReturnErrorIfNonExistingReview()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "get",
                    "9PN3ABCDEFGA",
                    "00000000-0000-0000-0000-000000000000"
                ],
                -1);

            result.Error.Should().Contain("Could not find review with ID");

            // No date option was passed, so no date parameters are sent and the service
            // searches every review. Suggesting --startDate here would misdirect the user.
            result.Error.Should().NotContain("--startDate");
        }

        [TestMethod]
        public async Task ReviewsGetCommandShouldSuggestWideningAnExplicitDateRange()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "get",
                    "9PN3ABCDEFGA",
                    "00000000-0000-0000-0000-000000000000",
                    "--startDate",
                    "2024-01-01"
                ],
                -1);

            result.Error.Should().Contain("within the requested date range");
        }

        [TestMethod]
        public async Task ReviewsGetCommandShouldNotClaimTheReviewIsMissingWhenTranslationFails()
        {
            FakeTranslationService
                .Setup(x => x.TranslateAsync(It.IsAny<IReadOnlyList<string?>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TranslationException("No Azure AI Translator key is configured."));

            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "get",
                    "9PN3ABCDEFGA",
                    FakeReviews[0].Id!,
                    "--translate"
                ],
                -1);

            result.Error.Should().Contain("No Azure AI Translator key is configured.");

            // The review was found; only the translation failed, so pointing the user at
            // --startDate would misdirect them.
            result.Error.Should().NotContain("Could not find review with ID");
        }

        [TestMethod]
        public async Task ReviewsGetCommandIsNotSupportedForUnpackagedApps()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "reviews",
                    "get",
                    Guid.Empty.ToString(),
                    FakeReviews[0].Id!
                ],
                -1);

            result.Error.Should().Contain("This command is not supported for unpackaged applications.");
        }
    }
}
