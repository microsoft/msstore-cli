// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using MSStore.API.Models;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class SubmissionCommandUnpackagedUnitTests : BaseCommandLineTest
    {
        [TestInitialize]
        public void Init()
        {
            FakeLogin();
            AddFakeAccount(null);
        }

        [TestMethod]
        public async Task UnpackagedSubmissionStatusCommand()
        {
            FakeStoreAPI
                .Setup(x => x.GetModuleStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseWrapper<ModuleStatus>
                {
                    ResponseData = new ModuleStatus
                    {
                        IsReady = true
                    }
                });

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "status",
                    Guid.Empty.ToString()
                ]);

            result.Output.Should().Contain("\"IsReady\": true,");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionGetCommand()
        {
            FakeStoreAPI
                .Setup(x => x.GetDraftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseWrapper<PackagesMetadataResponse>
                {
                    IsSuccess = true,
                    ResponseData = new PackagesMetadataResponse
                    {
                        Packages =
                            [
                                new Package
                                {
                                    PackageId = "12345"
                                }
                            ]
                    }
                });

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "get",
                    Guid.Empty.ToString()
                ]);

            result.Output.Should().Contain("\"PackageId\": \"12345\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionGetCommandShouldNotWrapJsonOutput()
        {
            // Longer than the width the test console renders at, and sprinkled with characters
            // that the serializer escapes as \uXXXX, so a wrap would both break the JSON and
            // corrupt the description.
            var longDescription = string.Concat(
                Enumerable.Repeat("Sync your mail & calendar across every device without a fuss. ", 12));

            FakeStoreAPI
                .Setup(x => x.GetDraftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseWrapper<ListingsMetadataResponse>
                {
                    IsSuccess = true,
                    ResponseData = new ListingsMetadataResponse
                    {
                        Listings =
                            [
                                new Listing
                                {
                                    Language = "en-us",
                                    Description = longDescription
                                }
                            ]
                    }
                });

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "get",
                    Guid.Empty.ToString()
                ]);

            using var json = JsonDocument.Parse(result.Output);

            json.RootElement
                .GetProperty("ResponseData")
                .GetProperty("Listings")[0]
                .GetProperty("Description")
                .GetString()
                .Should().Be(longDescription);
        }

        [TestMethod]
        public async Task UnpackagedSubmissionGetListingAssetsCommand()
        {
            FakeStoreAPI
                .Setup(x => x.GetDraftListingAssetsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new ListingAssetsResponse
                    {
                        ListingAssets =
                        [
                            new ListingAsset
                            {
                                Language = "en-us",
                                Screenshots =
                                [
                                    new Screenshot
                                    {
                                        AssetUrl = "https://www.example.com/screenshot.png",
                                        Id = "12345",
                                        ImageSize = new ImageSize
                                        {
                                            Height = 100,
                                            Width = 100
                                        }
                                    }
                                ]
                            }
                        ]
                    });

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "getListingAssets",
                    Guid.Empty.ToString()
                ]);

            result.Output.Should().Contain("\"AssetUrl\": \"https://www.example.com/screenshot.png\",");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionUpdateCommand()
        {
            FakeStoreAPI
                .Setup(x => x.UpdateProductPackagesAsync(It.IsAny<string>(), It.IsAny<UpdatePackagesRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateMetadataResponse
                {
                    OngoingSubmissionId = "12345",
                    PollingUrl = "https://www.example.com/polling"
                });

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    Guid.Empty.ToString(),
                    @"
{
""Packages"":
    [
        {
            ""PackageUrl"":""https://www.example.com/installer.exe""
        }
    ]
}"
                ]);

            result.Error.Should().Contain("Updating submission product");
            result.Output.Should().Contain("\"PollingUrl\": \"https://www.example.com/polling\"");
            result.Output.Should().Contain("\"OngoingSubmissionId\": \"12345\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionUpdateCommandWithPayloadOption()
        {
            FakeStoreAPI
                .Setup(x => x.UpdateProductPackagesAsync(It.IsAny<string>(), It.IsAny<UpdatePackagesRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateMetadataResponse
                {
                    OngoingSubmissionId = "12345",
                    PollingUrl = "https://www.example.com/polling"
                });

            var payloadFilePath = CreateTemporaryPayloadFile(
                @"
{
""Packages"":
    [
        {
            ""PackageUrl"":""https://www.example.com/installer.exe""
        }
    ]
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "update",
                    Guid.Empty.ToString(),
                    "--payload",
                    payloadFilePath
                ]);

            result.Error.Should().Contain("Updating submission product");
            result.Output.Should().Contain("\"PollingUrl\": \"https://www.example.com/polling\"");
            result.Output.Should().Contain("\"OngoingSubmissionId\": \"12345\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionUpdateMetadataCommand()
        {
            FakeStoreAPI
                .Setup(x => x.UpdateSubmissionMetadataAsync(It.IsAny<string>(), It.IsAny<UpdateMetadataRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateMetadataResponse
                {
                    OngoingSubmissionId = "12345",
                    PollingUrl = "https://www.example.com/polling"
                });

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "updateMetadata",
                    Guid.Empty.ToString(),
                    @"
{
""Availability"":
    {
        ""Pricing"":""1""
    }
}"
                ]);

            result.Error.Should().Contain("Updating submission metadata");
            result.Output.Should().Contain("\"PollingUrl\": \"https://www.example.com/polling\"");
            result.Output.Should().Contain("\"OngoingSubmissionId\": \"12345\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionUpdateMetadataCommandWithStandardInput()
        {
            FakeStoreAPI
                .Setup(x => x.UpdateSubmissionMetadataAsync(It.IsAny<string>(), It.IsAny<UpdateMetadataRequest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateMetadataResponse
                {
                    OngoingSubmissionId = "12345",
                    PollingUrl = "https://www.example.com/polling"
                });

            FakeConsole
                .Setup(x => x.ReadAllStandardInputAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    @"
{
""Availability"":
    {
        ""Pricing"":""1""
    }
}");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "updateMetadata",
                    Guid.Empty.ToString(),
                    "-"
                ]);

            result.Error.Should().Contain("Updating submission metadata");
            result.Output.Should().Contain("\"PollingUrl\": \"https://www.example.com/polling\"");
            result.Output.Should().Contain("\"OngoingSubmissionId\": \"12345\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionPublishCommand()
        {
            FakeStoreAPI
                .Setup(x => x.PublishSubmissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("12345");

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "publish",
                    Guid.Empty.ToString()
                ]);

            result.Error.Should().Contain("Published with Id");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionPollCommand()
        {
            FakeStoreAPI
                .Setup(x => x.GetModuleStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseWrapper<ModuleStatus>
                {
                    ResponseData = new ModuleStatus
                    {
                        OngoingSubmissionId = "12345",
                        IsReady = true
                    }
                });

            FakeStoreAPI
                .SetupSequence(x => x.GetSubmissionStatusPollingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseWrapper<SubmissionStatus>
                {
                    IsSuccess = true,
                    ResponseData = new SubmissionStatus
                    {
                        PublishingStatus = PublishingStatus.INPROGRESS,
                        HasFailed = false
                    }
                })
                .ReturnsAsync(new ResponseWrapper<SubmissionStatus>
                {
                    IsSuccess = true,
                    ResponseData = new SubmissionStatus
                    {
                        PublishingStatus = PublishingStatus.INPROGRESS,
                        HasFailed = false
                    }
                })
                .ReturnsAsync(new ResponseWrapper<SubmissionStatus>
                {
                    IsSuccess = true,
                    ResponseData = new SubmissionStatus
                    {
                        PublishingStatus = PublishingStatus.PUBLISHED,
                        HasFailed = false
                    }
                });

            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "poll",
                    Guid.Empty.ToString()
                ]);

            result.Error.Should().Contain("INPROGRESS");
            result.Error.Should().Contain("PUBLISHED");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionDeleteCommand()
        {
            var result = await ParseAndInvokeAsync(
                [
                    "submission",
                    "delete",
                    Guid.Empty.ToString()
                ], -1);

            result.Error.Should().Contain("This command is not supported for unpackaged applications.");
        }
    }
}