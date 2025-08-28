// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Packaged.Models;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class SubmissionCommandPackagedUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddFakeAccount(null);
            AddFakeApps();
            AddDefaultFakeSubmission();
        }

        [TestMethod]
        public async Task SubmissionCommandWithNoParameter()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "submission"
                ], 1);

            result.Output.Should().Contain("Executes commands to a store submission.");
        }

        [TestMethod]
        public async Task PackagedSubmissionStatusCommand()
        {
            FakeApps[0].LastPublishedApplicationSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            InitDefaultSubmissionStatusResponseQueue();

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "status",
                    FakeApps[0].Id!
                ]);

            result.Error.Should().Contain("Code1");
            result.Error.Should().Contain("Detail1");
        }

        [TestMethod]
        public async Task PackagedSubmissionGetCommand()
        {
            FakeApps[0].LastPublishedApplicationSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "get",
                    FakeApps[0].Id!
                ]);

            result.Output.Should().Contain("\"Id\": \"123456789\"");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionGetListingAssetsCommand()
        {
            FakeApps[0].LastPublishedApplicationSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "getListingAssets",
                    FakeApps[0].Id!
                ]);

            result.Output.Should().Contain("\"Description\": \"BaseListingDescription\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommand()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!,
                    @"
{
""ApplicationPackages"":
    [
        {
            ""FileName"":""C:\\temp\\installer.msix""
        }
    ]
}"
                ]);

            result.Error.Should().Contain("Updating submission product");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateMetadataCommand()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "updateMetadata",
                    FakeApps[0].Id!,
                    @"
{
""Listings"":
    {
        ""en-us"":
        {
            ""BaseListing"":
            {
                ""Description"": ""New description""
            }
        }
    }
}"
                ]);

            result.Error.Should().Contain("Updating submission product");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionPublishCommand()
        {
            FakeApps[0].LastPublishedApplicationSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            FakeStorePackagedAPI
                .Setup(x => x.CommitSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevCenterCommitResponse
                {
                    Status = "CommitStarted",
                });

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "publish",
                    FakeApps[0].Id!
                ]);

            result.Error.Should().Contain("Submission Commited with status");
        }

        [TestMethod]
        public async Task PackagedSubmissionPollCommand()
        {
            FakeApps[0].LastPublishedApplicationSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            InitDefaultSubmissionStatusResponseQueue();

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "poll",
                    FakeApps[0].Id!
                ]);

            result.Error.Should().Contain("Submission commit success!");
        }

        [TestMethod]
        public async Task PackagedSubmissionDeleteCommand()
        {
            FakeApps[0].PendingApplicationSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            FakeConsole
                .Setup(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "delete",
                    FakeApps[0].Id!
                ]);

            FakeConsole.Verify(x => x.YesNoConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

            result.Error.Should().Contain($"Found Pending Submission with Id '{FakeApps[0].PendingApplicationSubmission!.Id}'");
            result.Error.Should().Contain("Existing submission deleted!");
        }
    }
}