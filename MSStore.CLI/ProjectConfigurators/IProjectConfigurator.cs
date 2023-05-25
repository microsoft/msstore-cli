// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.ProjectConfigurators
{
    internal interface IProjectConfigurator
    {
        Task<bool> CanConfigureAsync(string pathOrUrl, CancellationToken ct);

        int? ValidateCommand(string pathOrUrl, DirectoryInfo? output, bool? commandPackage, bool? commandPublish);

        Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, Version? version, IStorePackagedAPI storePackagedAPI, CancellationToken ct);

        Task<List<string>?> GetAppImagesAsync(string pathOrUrl, CancellationToken ct);

        Task<List<string>?> GetDefaultImagesAsync(string pathOrUrl, CancellationToken ct);
    }
}
