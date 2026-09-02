// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text.Json;
using MSStore.API.Models;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Commands;

namespace MSStore.CLI.UnitTests
{
    /// <summary>
    /// Guards the base price across a publish.
    /// </summary>
    /// <remarks>
    /// Verified against the live submission API. On update, the base price behaves like this:
    /// <list type="bullet">
    /// <item><description><c>Free</c>/<c>Tier96</c>/<c>Tier1012</c>/<c>Tier1424</c> - <c>200 OK</c>, value preserved.</description></item>
    /// <item><description><c>Base</c> - <c>400</c> <c>'Base' is not a valid PriceId for base price.</c></description></item>
    /// <item><description>empty - <c>200 OK</c>, and the product silently becomes free. This is issue #112.</description></item>
    /// <item><description><c>pricing</c> omitted - <c>400</c> <c>Pricing data was not provided in the request.</c></description></item>
    /// </list>
    /// </remarks>
    [TestClass]
    public class PublishCommandPricingUnitTests : BaseCommandLineTest
    {
        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddDefaultFakeAccount();
            AddFakeApps();
        }

        private async Task<((string Output, string Error) Result, DevCenterSubmission? Sent)> PublishMsixAsync(
            Pricing? pricing,
            int? expectedExitCode = 0,
            params string[] extraArgs)
        {
            var path = CopyFilesRecursively("MSIXProject");
            var msixPath = Path.Combine(path, "test.msix");

            AddDefaultFakeSuccessfulSubmission(pricing);

            DevCenterSubmission? sent = null;
            FakeStorePackagedAPI
                .Setup(x => x.UpdateSubmissionAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DevCenterSubmission>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, DevCenterSubmission, CancellationToken>((_, _, s, _) => sent = s)
                .ReturnsAsync((string _, string _, DevCenterSubmission s, CancellationToken _) => s);

            string[] args = ["publish", msixPath, "--appId", FakeApps[0].Id!, "--verbose", .. extraArgs];

            var result = await ParseAndInvokeAsync(args, expectedExitCode);

            return (result, sent);
        }

        [TestMethod]
        [DataRow("Tier1012")]
        [DataRow("Tier1424")]
        [DataRow("Tier96")]
        [DataRow("Free")]
        public async Task PublishShouldSendThePriceBackUnchanged(string priceId)
        {
            // The core regression for issue #112: publishing must never alter the base price.
            var (result, sent) = await PublishMsixAsync(new Pricing { PriceId = priceId });

            result.Error.Should().Contain("Submission commit success! Here is some data:");

            sent.Should().NotBeNull();
            sent!.Pricing.Should().NotBeNull();
            sent.Pricing!.PriceId.Should().Be(priceId);
        }

        [TestMethod]
        public async Task PublishShouldNeverSendAnEmptyPrice()
        {
            // Commit 0b4b0bc set PriceId to null here. The API answers 200 OK and resets the
            // product to free, so this can only be caught by inspecting the outgoing payload.
            var (_, sent) = await PublishMsixAsync(
                new Pricing { PriceId = "Tier1012", IsAdvancedPricingModel = true });

            sent!.Pricing!.PriceId.Should().NotBeNullOrWhiteSpace();
            sent.Pricing.PriceId.Should().Be("Tier1012");
        }

        [TestMethod]
        public async Task PublishShouldIgnoreIsAdvancedPricingModel()
        {
            // isAdvancedPricingModel only describes which tier range a dashboard offers, and the
            // API reports it inconsistently for the same product. It must not drive any decision.
            var (result, sent) = await PublishMsixAsync(
                new Pricing { PriceId = "Tier1012", IsAdvancedPricingModel = true });

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            sent!.Pricing!.PriceId.Should().Be("Tier1012");
        }

        [TestMethod]
        public async Task PublishShouldStopWhenThePriceCannotBePreserved()
        {
            var (result, sent) = await PublishMsixAsync(new Pricing { PriceId = "Base" }, -1);

            result.Error.Should().Contain("Could not preserve this product's price");
            result.Error.Should().Contain("--priceId");

            // Nothing may be sent, otherwise the product would be reset to free.
            sent.Should().BeNull();

            FakeStorePackagedAPI
                .Verify(
                    x => x.DeleteSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                    Times.Once);
        }

        [TestMethod]
        public async Task PublishShouldNotClaimOnlyFreeProductsAreSupported()
        {
            // The old message said "App updates are supported only for Free products", which is
            // wrong: a product with a real tier publishes fine, as the tests above show.
            var (result, _) = await PublishMsixAsync(new Pricing { PriceId = "Base" }, -1);

            result.Error.Should().NotContain("only for Free products");
        }

        [TestMethod]
        public async Task PublishWithPriceIdShouldRecoverAProductWhoseBasePriceIsNotRoundTrippable()
        {
            var (result, sent) = await PublishMsixAsync(
                new Pricing { PriceId = "Base" },
                0,
                "--priceId",
                "Tier1012");

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            sent!.Pricing!.PriceId.Should().Be("Tier1012");
        }

        [TestMethod]
        public async Task PublishWithPriceIdShouldOverrideAnExistingTier()
        {
            var (_, sent) = await PublishMsixAsync(
                new Pricing { PriceId = "Tier1012" },
                0,
                "--priceId",
                "Tier1424");

            sent!.Pricing!.PriceId.Should().Be("Tier1424");
        }

        [TestMethod]
        public async Task PublishShouldSucceedWhenTheProductHasNoPricingAtAll()
        {
            var (result, sent) = await PublishMsixAsync(null);

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            sent!.Pricing.Should().BeNull();
        }

        [TestMethod]
        public async Task PublishWithPriceIdShouldApplyEvenWhenTheProductHasNoPricingAtAll()
        {
            // The API rejects an update that omits pricing, so an explicit price has to be
            // materialized rather than silently dropped.
            var (result, sent) = await PublishMsixAsync(null, 0, "--priceId", "Tier1012");

            result.Error.Should().Contain("Submission commit success! Here is some data:");
            sent!.Pricing.Should().NotBeNull();
            sent.Pricing!.PriceId.Should().Be("Tier1012");
        }

        [TestMethod]
        public void PricingMustAlwaysSerializeThePriceIdProperty()
        {
            // Guards against "tidying" the payload by omitting a null PriceId. Update has no
            // patch semantics: a pricing object with the property removed is accepted with
            // 200 OK and turns the product free, exactly like sending it as null. Adding
            // JsonIgnore(WhenWritingNull) to Pricing.PriceId would silently reintroduce #112.
            var json = JsonSerializer.Serialize(
                new DevCenterSubmission { Pricing = new Pricing { PriceId = null } },
                SourceGenerationContext.GetCustom().DevCenterSubmission);

            json.Should().Contain("\"PriceId\"");
        }

        [TestMethod]
        public void PricingShouldSerializeARealTierVerbatim()
        {
            var json = JsonSerializer.Serialize(
                new DevCenterSubmission { Pricing = new Pricing { PriceId = "Tier1012" } },
                SourceGenerationContext.GetCustom().DevCenterSubmission);

            json.Should().Contain("\"PriceId\":\"Tier1012\"");
        }

        private static ParseResult ParsePublish(params string[] args) =>
            new PublishCommand().Parse(args);

        [TestMethod]
        public void PublishCommandPriceIdShouldDefaultToNullWhenOmitted()
        {
            ParsePublish("publish", ".")
                .GetValue(PublishCommand.PriceIdOption)
                .Should()
                .BeNull();
        }

        [TestMethod]
        [DataRow("Tier1012", "Tier1012")]
        [DataRow("tier1012", "Tier1012")]
        [DataRow("TIER1012", "Tier1012")]
        [DataRow(" Tier1012 ", "Tier1012")]
        [DataRow("Free", "Free")]
        [DataRow("free", "Free")]
        [DataRow("NotAvailable", "NotAvailable")]
        [DataRow("Tier2", "Tier2")]
        public void PublishCommandPriceIdShouldNormalizeAcceptedValues(string input, string expected)
        {
            var parseResult = ParsePublish("publish", ".", "--priceId", input);

            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValue(PublishCommand.PriceIdOption).Should().Be(expected);
        }

        [TestMethod]
        [DataRow("Base")]
        [DataRow("Tier")]
        [DataRow("1012")]
        [DataRow("TierAbc")]
        [DataRow("Tier-1")]
        [DataRow("not-a-tier")]
        public void PublishCommandPriceIdShouldRejectValuesTheApiWouldNotAccept(string input)
        {
            // "Base" is rejected on purpose: it is exactly the value that cannot be sent back.
            ParsePublish("publish", ".", "--priceId", input)
                .Errors
                .Should()
                .ContainSingle()
                .Which
                .Message
                .Should()
                .Be("Invalid price id. The value must be 'Free', 'NotAvailable', or a tier such as 'Tier1012'.");
        }
    }
}
