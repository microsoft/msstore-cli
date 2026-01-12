// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal interface IAzureBlobManager
    {
        Task<string> UploadFileAsync(string blobUri, string localFilePath, IProgress<double> progress, long uploadTimeout, CancellationToken ct);
    }
}
