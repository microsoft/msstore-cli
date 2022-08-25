// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services.PartnerCenter;

namespace MSStore.CLI.Commands.Init.Setup
{
    internal interface IProjectConfigurator
    {
        public string ConfiguratorProjectType { get; }

        public bool CanConfigure(string pathOrUrl);

        Task<int> ConfigureAsync(string pathOrUrl, AccountEnrollment account, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct);
    }
}
