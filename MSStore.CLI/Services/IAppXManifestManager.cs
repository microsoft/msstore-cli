// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.Services
{
    internal interface IAppXManifestManager
    {
        void UpdateManifest(string appxManifestPath, DevCenterApplication app, string publisherDisplayName, Version? version);
        void MinimalUpdateManifest(string appxManifestPath, DevCenterApplication app, string publisherDisplayName);
        Version UpdateManifestVersion(string appxManifestPath, Version? version);
        string? GetAppId(FileInfo fileInfo);
        List<string> GetAllImagesFromManifest(FileInfo appxManifest, ILogger logger);
    }
}
