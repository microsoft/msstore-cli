// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal interface IFileDownloader
    {
        Task<bool> DownloadAsync(string url, string destinationFileName, IProgress<double> progress, CancellationToken ct = default);
    }
}
