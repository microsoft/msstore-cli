// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.Commands.Init.Setup
{
    internal interface IProjectPackager
    {
        Task<int> PackageAsync(string pathOrUrl, DevCenterApplication? app, IStorePackagedAPI storePackagedAPI, CancellationToken ct);
    }
}
