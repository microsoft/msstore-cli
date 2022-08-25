// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.Services
{
    internal interface IZipFileManager
    {
        void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName);
        void ExtractZip(string sourceArchiveFileName, string destinationDirectoryName);
    }
}
