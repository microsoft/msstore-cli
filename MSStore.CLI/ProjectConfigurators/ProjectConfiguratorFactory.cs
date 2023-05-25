// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public async Task<IProjectConfigurator?> FindProjectConfiguratorAsync(string pathOrUrl, CancellationToken ct)
        {
            var projectConfigurators = _serviceProvider.GetServices<IProjectConfigurator>();
            foreach (var projectConfigurator in projectConfigurators)
            {
                if (await projectConfigurator.CanConfigureAsync(pathOrUrl, ct))
                {
                    return projectConfigurator;
                }
            }

            return null;
        }

        public async Task<IProjectPublisher?> FindProjectPublisherAsync(string pathOrUrl, CancellationToken ct)
        {
            var projectPublishers = _serviceProvider.GetServices<IProjectPublisher>().ToList();
            projectPublishers.AddRange(_serviceProvider.GetServices<IProjectConfigurator>()
                .Where(c => c is IProjectPublisher projectPublisher &&
                    !projectPublishers.Contains(projectPublisher))
                .Cast<IProjectPublisher>());
            foreach (var projectPublisher in projectPublishers)
            {
                if (await projectPublisher.CanPublishAsync(pathOrUrl, ct))
                {
                    return projectPublisher;
                }
            }

            return null;
        }
    }
}
