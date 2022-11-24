// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.IO.Compression;

namespace MSStore.CLI.Services
{
    internal class ZipFileManager : IZipFileManager
    {
        public void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName)
        {
            if (File.Exists(destinationArchiveFileName))
            {
                File.Delete(destinationArchiveFileName);
            }

            ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, CompressionLevel.Optimal, false);
        }

        public void ExtractZip(string sourceArchiveFileName, string destinationDirectoryName)
        {
            ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName, true);
        }
    }
}
