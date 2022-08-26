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
                new[]
                {
                    "submission"
                });

            result.Should().Contain("Executes commands to a store submission.");
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
                new[]
                {
                    "submission",
                    "status",
                    FakeApps[0].Id!
                });

            result.Should().Contain("\"Code\": \"Code1\"");
            result.Should().Contain("\"Details\": \"Detail1\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionGetCommand()
        {
            FakeApps[0].LastPublishedApplicationSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "get",
                    FakeApps[0].Id!
                });

            result.Should().Contain("\"Id\": \"123456789\"");
            result.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionGetListingAssetsCommand()
        {
            FakeApps[0].LastPublishedApplicationSubmission = new ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "getListingAssets",
                    FakeApps[0].Id!
                });

            result.Should().Contain("\"Description\": \"BaseListingDescription\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommand()
        {
            var result = await ParseAndInvokeAsync(
                new[]
                {
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
                });

            result.Should().Contain("Updating submission product");
            result.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateMetadataCommand()
        {
            var result = await ParseAndInvokeAsync(
                new[]
                {
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
                });

            result.Should().Contain("Updating submission product");
            result.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
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
                new[]
                {
                    "submission",
                    "publish",
                    FakeApps[0].Id!
                });

            result.Should().Contain("Submission Commited with status");
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
                new[]
                {
                    "submission",
                    "poll",
                    FakeApps[0].Id!
                });

            result.Should().Contain("Submission commit success!");
        }
    }
}