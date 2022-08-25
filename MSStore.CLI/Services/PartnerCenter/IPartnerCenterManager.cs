// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.PartnerCenter
{
    internal interface IPartnerCenterManager
    {
        Task<AccountEnrollments> GetEnrollmentAccountsAsync(CancellationToken ct);
    }
}
