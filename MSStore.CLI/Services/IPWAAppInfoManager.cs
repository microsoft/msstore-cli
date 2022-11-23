// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MSStore.CLI.Services.PWABuilder;

namespace MSStore.CLI.Services
{
    internal interface IPWAAppInfoManager
    {
        Task SaveAsync(PWAAppInfo pwaAppInfo, string directoryPath, CancellationToken ct);
        Task<PWAAppInfo> LoadAsync(string directoryPath, CancellationToken ct);
    }
}
