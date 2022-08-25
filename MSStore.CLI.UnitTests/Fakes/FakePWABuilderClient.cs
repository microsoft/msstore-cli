// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.PWABuilder;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakePWABuilderClient : IPWABuilderClient
    {
        public Task<string> GenerateZipAsync(GenerateZipRequest generateZipRequest, IProgress<double> progress, CancellationToken ct)
        {
            progress.Report(0);

            var rootDir = Path.Combine(Path.GetTempPath(), "MSStore", "PWAZips");

            Directory.CreateDirectory(rootDir);

            var filePath = Path.Combine(rootDir, Path.ChangeExtension(Path.GetRandomFileName(), "zip"));

            progress.Report(100);

            return Task.FromResult(filePath);
        }

        public Task<WebManifestFetchResponse> FetchWebManifestAsync(Uri site, CancellationToken ct)
        {
            return Task.FromResult(new WebManifestFetchResponse
            {
                Content = new WebManifestFetchContent
                {
                    Json = new WebManifestJson
                    {
                        Description = "Test description",
                        Screenshots = new List<ScreenShot>
                        {
                            new ScreenShot
                            {
                                Src = "https://www.microsoft.com/image1.png"
                            }
                        },
                        Icons = new List<Icon>
                        {
                            new Icon
                            {
                                Src = "https://www.microsoft.com/image2.png",
                                Sizes = "512x512"
                            },
                            new Icon
                            {
                                Src = "https://www.microsoft.com/image3.png",
                                Sizes = "6x5"
                            }
                        }
                    }
                },
            });
        }
    }
}
