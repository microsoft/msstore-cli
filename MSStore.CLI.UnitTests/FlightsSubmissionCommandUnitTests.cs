// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Packaged.Models;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class FlightsSubmissionCommandUnitTests : BaseCommandLineTest
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
        public async Task FlightSubmissionCommandWithNoParameter()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission"
                ], 1);

            result.Output.Should().Contain("Execute flight submissions related tasks.");
        }

        [TestMethod]
        public async Task PackagedFlightSubmissionStatusCommand()
        {
            FakeFlights[0].LastPublishedFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            InitDefaultFlightSubmissionStatusResponseQueue();

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "status",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Error.Should().Contain("Code1");
            result.Error.Should().Contain("Detail1");
        }

        [TestMethod]
        public async Task PackagedFlightSubmissionGetCommand()
        {
            FakeFlights[0].LastPublishedFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "get",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Output.Should().Contain("\"Id\": \"123456789\"");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedFlightSubmissionUpdateCommand()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "update",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!,
                    @"
{
""FlightPackages"":
    [
        {
            ""FileName"":""C:\\temp\\installer.msix""
        }
    ]
}"
                ]);

            result.Error.Should().Contain("Updating flight submission product");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedFlightSubmissionPublishCommand()
        {
            FakeFlights[0].PendingFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            FakeStorePackagedAPI
                .Setup(x => x.CommitFlightSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevCenterCommitResponse
                {
                    Status = "CommitStarted",
                });

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "publish",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Error.Should().Contain("Flight Submission Commited with status");
        }

        [TestMethod]
        public async Task PackagedFlightSubmissionPollCommand()
        {
            FakeFlights[0].PendingFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            InitDefaultFlightSubmissionStatusResponseQueue();

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "poll",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            result.Error.Should().Contain("Submission commit success!");
        }

        [TestMethod]
        public async Task PackagedFlightSubmissionDeleteCommand()
        {
            FakeFlights[0].PendingFlightSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            FakeConsole
                .Setup(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await ParseAndInvokeAsync(
                [
                    "flights",
                    "submission",
                    "delete",
                    FakeApps[0].Id!,
                    FakeFlights[0].FlightId!
                ]);

            FakeConsole.Verify(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

            result.Error.Should().Contain($"Found Flight Submission with Id '{FakeFlights[0].PendingFlightSubmission!.Id}'");
            result.Error.Should().Contain("Existing submission deleted!");
        }
    }
}