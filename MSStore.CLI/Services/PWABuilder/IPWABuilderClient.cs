// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.PWABuilder
{
    internal interface IPWABuilderClient
    {
        Task<string> GenerateZipAsync(GenerateZipRequest generateZipRequest, IProgress<double> progress, CancellationToken ct);
        Task<WebManifestFetchResponse> FetchWebManifestAsync(Uri site, CancellationToken ct);
    }
}
