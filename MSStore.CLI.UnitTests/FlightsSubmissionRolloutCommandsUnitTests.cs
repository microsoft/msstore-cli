// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Packaged.Models;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class FlightsSubmissionRolloutCommandsUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddFakeAccount(null);
            AddFakeApps();
            AddFakeFlights();
            AddDefaultFakeFlightSubmission();
        }

        [TestMethod]
        public async Task FlightSubmissionRolloutGetCommandDoesntWorkIfNoSubmission()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "rollout",
                    "get",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ], -1);

            result.Error.Should().Contain("Could not find the flight submission. Please check the ProductId/FlightId");
        }

        [TestMethod]
        public async Task FlightSubmissionRolloutGetCommand()
        {
            FakeFlights[0].LastPublishedFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            FakeStorePackagedAPI
                .Setup(x => x.GetPackageRolloutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PackageRollout
                {
                    IsPackageRollout = true,
                    PackageRolloutPercentage = 0,
                    FallbackSubmissionId = "0",
                    PackageRolloutStatus = "PackageRolloutNotStarted"
                });

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "rollout",
                    "get",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Output.Should().Contain("\"PackageRolloutStatus\": \"PackageRolloutNotStarted\"");
        }

        [TestMethod]
        public async Task FlightSubmissionRolloutUpdateCommand()
        {
            FakeFlights[0].LastPublishedFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            FakeStorePackagedAPI
                .Setup(x => x.UpdatePackageRolloutPercentageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PackageRollout
                {
                    IsPackageRollout = true,
                    PackageRolloutPercentage = 100,
                    FallbackSubmissionId = "0",
                    PackageRolloutStatus = "PackageRolloutNotStarted"
                });

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "rollout",
                    "update",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!,
                    "100"
                ]);

            result.Output.Should().Contain("\"PackageRolloutPercentage\": 100");
        }

        [TestMethod]
        public async Task FlightSubmissionRolloutHaltCommand()
        {
            FakeFlights[0].LastPublishedFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            FakeStorePackagedAPI
                .Setup(x => x.HaltPackageRolloutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PackageRollout
                {
                    IsPackageRollout = true,
                    PackageRolloutPercentage = 0,
                    FallbackSubmissionId = "0",
                    PackageRolloutStatus = "PackageRolloutStopped"
                });

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "rollout",
                    "halt",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Output.Should().Contain("\"PackageRolloutStatus\": \"PackageRolloutStopped\"");
        }

        [TestMethod]
        public async Task FlightSubmissionRolloutFinalizeCommand()
        {
            FakeFlights[0].LastPublishedFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            FakeStorePackagedAPI
                .Setup(x => x.FinalizePackageRolloutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PackageRollout
                {
                    IsPackageRollout = true,
                    PackageRolloutPercentage = 0,
                    FallbackSubmissionId = "0",
                    PackageRolloutStatus = "PackageRolloutComplete"
                });

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "rollout",
                    "finalize",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Output.Should().Contain("\"PackageRolloutStatus\": \"PackageRolloutComplete\"");
        }
    }
}
