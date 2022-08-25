// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeAzureBlobManager : IAzureBlobManager
    {
        public Task<string> UploadFileAsync(string blobUri, string localFilePath, IProgress<double> progress, CancellationToken ct)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
