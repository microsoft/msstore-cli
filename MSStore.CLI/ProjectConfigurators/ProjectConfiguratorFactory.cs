// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class ProjectConfiguratorFactory : IProjectConfiguratorFactory
    {
        private IServiceProvider _serviceProvider;

        public ProjectConfiguratorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IProjectConfigurator? FindProjectConfigurator(string pathOrUrl) =>
            _serviceProvider.GetServices<IProjectConfigurator>().FirstOrDefault(
                x => x.CanConfigure(pathOrUrl));
    }
}
