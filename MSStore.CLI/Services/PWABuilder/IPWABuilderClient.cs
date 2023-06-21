// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.PWABuilder
{
    internal interface IPWABuilderClient
    {
        Task GenerateZipAsync(GenerateZipRequest generateZipRequest, string outputZipPath, IProgress<double> progress, CancellationToken ct);
        Task<WebManifestFindResponse> FindWebManifestAsync(Uri site, CancellationToken ct);
    }
}
