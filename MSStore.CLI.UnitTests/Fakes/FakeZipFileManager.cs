// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeZipFileManager : IZipFileManager
    {
        public void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName)
        {
        }

        public void ExtractZip(string sourceArchiveFileName, string destinationDirectoryName)
        {
        }
    }
}
