// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal interface INuGetPackageManager
    {
        Task<bool> IsPackageInstalledAsync(DirectoryInfo directory, string packageName, CancellationToken ct);
    }
}
