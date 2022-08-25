// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MSStore.API;
using MSStore.API.Models;
using MSStore.API.Packaged;

namespace MSStore.CLI.Services
{
    internal interface IStoreAPIFactory
    {
        Task<IStoreAPI> CreateAsync(Configurations? config = null, CancellationToken ct = default);
        Task<IStorePackagedAPI> CreatePackagedAsync(Configurations? config = null, CancellationToken ct = default);
    }
}
