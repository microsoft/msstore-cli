// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
                new[]
                {
                    "submission",
                    "status",
                    Guid.Empty.ToString()
                });

            result.Should().Contain("\"IsReady\": true,");
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
                        Packages = new List<Package>
                            {
                                new Package
                                {
                                    PackageId = "12345"
                                }
                            }
                    }
                });

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "get",
                    Guid.Empty.ToString()
                });

            result.Should().Contain("\"PackageId\": \"12345\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionGetListingAssetsCommand()
        {
            FakeStoreAPI
                .Setup(x => x.GetDraftListingAssetsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new ListingAssetsResponse
                    {
                        ListingAssets = new List<ListingAsset>
                        {
                            new ListingAsset
                            {
                                Language = "en-us",
                                Screenshots = new List<Screenshot>
                                {
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
                                }
                            }
                        }
                    });

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "getListingAssets",
                    Guid.Empty.ToString()
                });

            result.Should().Contain("\"AssetUrl\": \"https://www.example.com/screenshot.png\",");
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
                new[]
                {
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
                });

            result.Should().Contain("Updating submission product");
            result.Should().Contain("\"PollingUrl\": \"https://www.example.com/polling\"");
            result.Should().Contain("\"OngoingSubmissionId\": \"12345\"");
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
                new[]
                {
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
                });

            result.Should().Contain("Updating submission metadata");
            result.Should().Contain("\"PollingUrl\": \"https://www.example.com/polling\"");
            result.Should().Contain("\"OngoingSubmissionId\": \"12345\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionPublishCommand()
        {
            FakeStoreAPI
                .Setup(x => x.PublishSubmissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("12345");

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "publish",
                    Guid.Empty.ToString()
                });

            result.Should().Contain("Published with Id");
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
                new[]
                {
                    "submission",
                    "poll",
                    Guid.Empty.ToString()
                });

            result.Should().Contain("INPROGRESS");
            result.Should().Contain("PUBLISHED");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionDeleteCommand()
        {
            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "delete",
                    Guid.Empty.ToString()
                }, -1);

            result.Should().Contain("This command is not supported for unpackaged applications.");
        }
    }
}