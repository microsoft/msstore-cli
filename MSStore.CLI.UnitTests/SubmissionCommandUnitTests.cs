// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using MSStore.API.Models;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class SubmissionCommandUnitTests : BaseCommandLineTest
    {
        [TestMethod]
        public async Task SubmissionCommandWithNoParameter()
        {
            FakeLogin();

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission"
                });

            result.Should().Contain("Executes commands to a store submission.");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionStatusCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeStoreAPIFactory.FakeStoreAPI.ModuleStatus =
                new ResponseWrapper<ModuleStatus>
                {
                    ResponseData = new ModuleStatus
                    {
                        IsReady = true
                    }
                };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "status",
                    Guid.Empty.ToString()
                },
                () =>
                {
                    AddFakeAccount(null);
                    return Task.CompletedTask;
                });

            result.Should().Contain("\"IsReady\": true,");
        }

        [TestMethod]
        public async Task PackagedSubmissionStatusCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeApps[0].LastPublishedApplicationSubmission = new API.Packaged.Models.ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "status",
                    FakeApps[0].Id!
                },
                () =>
                {
                    AddFakeAccount(null);
                    AddFakeApps();
                    AddDefaultFakeSubmission();
                    FakeStoreAPIFactory.FakeStorePackagedAPI.InitDefaultSubmissionStatusResponseQueue();

                    return Task.CompletedTask;
                });

            result.Should().Contain("\"Code\": \"Code1\"");
            result.Should().Contain("\"Details\": \"Detail1\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionGetCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeStoreAPIFactory.FakeStoreAPI.Draft =
                new ResponseWrapper<PackagesMetadataResponse>
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
                };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "get",
                    Guid.Empty.ToString()
                },
                () =>
                {
                    AddFakeAccount(null);
                    return Task.CompletedTask;
                });

            result.Should().Contain("\"PackageId\": \"12345\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionGetCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeApps[0].LastPublishedApplicationSubmission = new API.Packaged.Models.ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "get",
                    FakeApps[0].Id!
                },
                () =>
                {
                    AddFakeAccount(null);
                    AddFakeApps();
                    AddDefaultFakeSubmission();

                    return Task.CompletedTask;
                });

            result.Should().Contain("\"Id\": \"123456789\"");
            result.Should().Contain("\"FileUploadUrl\": \"https://azureblob.com/fileupload\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionGetListingAssetsCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeStoreAPIFactory.FakeStoreAPI.ListingAssetsResponse =
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
                };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "getListingAssets",
                    Guid.Empty.ToString()
                },
                () =>
                {
                    AddFakeAccount(null);
                    return Task.CompletedTask;
                });

            result.Should().Contain("\"AssetUrl\": \"https://www.example.com/screenshot.png\",");
        }

        [TestMethod]
        public async Task PackagedSubmissionGetListingAssetsCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeApps[0].LastPublishedApplicationSubmission = new API.Packaged.Models.ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "getListingAssets",
                    FakeApps[0].Id!
                },
                () =>
                {
                    AddFakeAccount(null);
                    AddFakeApps();
                    AddDefaultFakeSubmission();

                    return Task.CompletedTask;
                });

            result.Should().Contain("\"Description\": \"BaseListingDescription\"");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionUpdateCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeStoreAPIFactory.FakeStoreAPI.UpdateMetadataResponse = new UpdateMetadataResponse
            {
                OngoingSubmissionId = "12345",
                PollingUrl = "https://www.example.com/polling"
            };

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
                },
                () =>
                {
                    AddFakeAccount(null);
                    return Task.CompletedTask;
                });

            result.Should().Contain("Updating submission product");
            result.Should().Contain("\"PollingUrl\": \"https://www.example.com/polling\"");
            result.Should().Contain("\"OngoingSubmissionId\": \"12345\"");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

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
                },
                () =>
                {
                    AddFakeAccount(null);
                    AddFakeApps();
                    AddDefaultFakeSubmission();

                    return Task.CompletedTask;
                });

            result.Should().Contain("Updating submission product");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionUpdateMetadataCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeStoreAPIFactory.FakeStoreAPI.UpdateMetadataResponse = new UpdateMetadataResponse
            {
                OngoingSubmissionId = "12345",
                PollingUrl = "https://www.example.com/polling"
            };

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
                },
                () =>
                {
                    AddFakeAccount(null);
                    return Task.CompletedTask;
                });

            result.Should().Contain("Updating submission metadata");
        }

        [TestMethod]
        public async Task PackagedSubmissionUpdateMetadataCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "updateMetadata",
                    FakeApps[0].Id!,
                    "TODO"
                },
                () =>
                {
                    AddFakeAccount(null);
                    AddFakeApps();
                    AddDefaultFakeSubmission();

                    return Task.CompletedTask;
                }, -2);
        }

        [TestMethod]
        public async Task UnpackagedSubmissionPublishCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "publish",
                    Guid.Empty.ToString(),
                    "12345"
                },
                () =>
                {
                    AddFakeAccount(null);
                    return Task.CompletedTask;
                });

            result.Should().Contain("Published with Id");
        }

        [TestMethod]
        public async Task PackagedSubmissionPublishCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeApps[0].LastPublishedApplicationSubmission = new API.Packaged.Models.ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "publish",
                    FakeApps[0].Id!,
                    "12345"
                },
                () =>
                {
                    AddFakeAccount(null);
                    AddFakeApps();
                    AddDefaultFakeSubmission();

                    return Task.CompletedTask;
                });

            result.Should().Contain("Submission Commited with status");
        }

        [TestMethod]
        public async Task UnpackagedSubmissionPollCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeStoreAPIFactory.FakeStoreAPI.PublishingStatus = PublishingStatus.PUBLISHED;

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "poll",
                    Guid.Empty.ToString(),
                    "12345"
                },
                () =>
                {
                    AddFakeAccount(null);
                    return Task.CompletedTask;
                });

            result.Should().Contain("Polling submission status");
        }

        [TestMethod]
        public async Task PackagedSubmissionPollCommand()
        {
            FakeLogin();
            await FakeStoreAPIFactory.InitAsync(ct: CancellationToken.None);

            FakeApps[0].LastPublishedApplicationSubmission = new API.Packaged.Models.ApplicationSubmissionInfo
            {
                Id = "123456789"
            };

            var result = await ParseAndInvokeAsync(
                new[]
                {
                    "submission",
                    "poll",
                    FakeApps[0].Id!,
                    "12345"
                },
                () =>
                {
                    AddFakeAccount(null);
                    AddFakeApps();
                    AddDefaultFakeSubmission();

                    return Task.CompletedTask;
                }, -2);
        }
    }
}