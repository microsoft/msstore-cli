// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.ProjectConfigurators
{
    internal interface IProjectConfiguratorFactory
    {
        IProjectConfigurator? FindProjectConfigurator(string pathOrUrl);
    }
}