// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.ProjectConfigurators
{
    internal interface IProjectPublisher
    {
        Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct);
        string[] PackageFilesExtensionInclude { get; }
        string[]? PackageFilesExtensionExclude { get; }
        SearchOption PackageFilesSearchOption { get; }
        string OutputSubdirectory { get; }
        string DefaultInputSubdirectory { get; }
        Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? inputDirectory, IStorePackagedAPI storePackagedAPI, CancellationToken ct);
    }
}
