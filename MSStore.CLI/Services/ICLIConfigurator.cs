// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace MSStore.CLI.Services
{
    internal interface ICLIConfigurator
    {
        Task<bool> ConfigureAsync(IAnsiConsole ansiConsole, bool askConfirmation, Guid? tenantId = null, string? sellerId = null, Guid? clientId = null, string? clientSecret = null, string? certificateThumbprint = null, string? certificateFilePath = null, string? certificatePassword = null, CancellationToken ct = default);
        Task<bool> ResetAsync(CancellationToken ct = default);
    }
}
