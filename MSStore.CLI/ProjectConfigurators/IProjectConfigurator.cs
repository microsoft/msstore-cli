// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.ProjectConfigurators
{
    internal interface IProjectConfigurator
    {
        string ConfiguratorProjectType { get; }

        bool CanConfigure(string pathOrUrl);

        int? ValidateCommand(string pathOrUrl, DirectoryInfo? output, bool? commandPackage, bool? commandPublish);

        Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct);
    }
}
