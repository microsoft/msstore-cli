// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
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
        public async Task PackagedSubmissionGetCommandShouldNotWrapJsonOutput()
        {
            // Longer than the width the test console renders at, and sprinkled with characters
            // that the serializer escapes as \uXXXX, so a wrap would both break the JSON and
            // corrupt the description.
            var longDescription = string.Concat(
                Enumerable.Repeat("Sync your mail & calendar across every device without a fuss. ", 12));

            AddDefaultFakeSubmission(longDescription);

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

            using var json = JsonDocument.Parse(result.Output);

            json.RootElement
                .GetProperty("Listings")
                .GetProperty("en-us")
                .GetProperty("BaseListing")
                .GetProperty("Description")
                .GetString()
                .Should().Be(longDescription);
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
        public async Task PackagedSubmissionUpdateCommandWithPayloadOption()
        {
            var payloadFilePath = CreateTemporaryPayloadFile(
                @"
{
""ApplicationPackages"":
    [
        {
            ""FileName"":""C:\\temp\\installer.msix""
        }
    ]
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!,
                    "--payload",
                    payloadFilePath
                ]);

            result.Error.Should().Contain("Updating submission product");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommandWithFilePathArgument()
        {
            var payloadFilePath = CreateTemporaryPayloadFile(
                @"
{
""ApplicationPackages"":
    [
        {
            ""FileName"":""C:\\temp\\installer.msix""
        }
    ]
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!,
                    payloadFilePath
                ]);

            result.Error.Should().Contain("Updating submission product");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommandWithStandardInputArgument()
        {
            FakeConsole
                .Setup(x => x.ReadAllStandardInputAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    @"
{
""ApplicationPackages"":
    [
        {
            ""FileName"":""C:\\temp\\installer.msix""
        }
    ]
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!,
                    "-"
                ]);

            result.Error.Should().Contain("Updating submission product");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommandWithRedirectedStandardInput()
        {
            FakeConsole
                .Setup(x => x.IsInputRedirected)
                .Returns(true);
            FakeConsole
                .Setup(x => x.ReadAllStandardInputAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    @"
{
""ApplicationPackages"":
    [
        {
            ""FileName"":""C:\\temp\\installer.msix""
        }
    ]
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!
                ]);

            result.Error.Should().Contain("Updating submission product");
            result.Output.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommandWithNoPayload()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!
                ], 1);

            result.Error.Should().Contain("No 'product' was provided.");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommandWithUnknownFilePath()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!,
                    "this-file-does-not-exist.json"
                ], 1);

            result.Error.Should().Contain("is neither a JSON payload nor a path to an existing file");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommandWithBothInlineJsonAndPayloadOption()
        {
            var payloadFilePath = CreateTemporaryPayloadFile(
                @"
{
""ApplicationPackages"":
    [
        {
            ""FileName"":""C:\\temp\\installer.msix""
        }
    ]
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!,
                    @"{ ""ApplicationPackages"": [] }",
                    "--payload",
                    payloadFilePath
                ], 1);

            result.Error.Should().Contain("Use only one of them.");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommandWithPayloadBiggerThanTheCommandLineLimit()
        {
            // The maximum command line length on Windows is 32,767 characters, so a payload this
            // big can only be provided through a file or through the standard input stream.
            var description = new string('a', 40000);

            var payloadFilePath = CreateTemporaryPayloadFile(
                @"
{
""Listings"":
    {
        ""en-us"":
        {
            ""BaseListing"":
            {
                ""Description"": """ + description + @"""
            }
        }
    }
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    FakeApps[0].Id!,
                    "--payload",
                    payloadFilePath
                ]);

            FakeStorePackagedAPI.Verify(
                x => x.UpdateSubmissionAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<DevCenterSubmission>(s => s.Listings!["en-us"].BaseListing!.Description == description),
                    It.IsAny<CancellationToken>()),
                Times.Once);

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
        public async Task PackagedSubmissionUpdateMetadataCommandWithPayloadOption()
        {
            var payloadFilePath = CreateTemporaryPayloadFile(
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
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "updateMetadata",
                    FakeApps[0].Id!,
                    "-p",
                    payloadFilePath
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

            result.Error.Should().Contain("Submission Committed with status");
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