// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeBrowserLauncher : IBrowserLauncher
    {
        public List<string> OpennedUrls { get; } = new List<string>();

        public void OpenBrowser(string url)
        {
            OpennedUrls.Add(url);
        }
    }
}