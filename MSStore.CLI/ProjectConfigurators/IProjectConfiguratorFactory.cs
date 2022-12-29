// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.ProjectConfigurators
{
    internal interface IProjectConfiguratorFactory
    {
        Task<IProjectConfigurator?> FindProjectConfiguratorAsync(string pathOrUrl, CancellationToken ct);
    }
}