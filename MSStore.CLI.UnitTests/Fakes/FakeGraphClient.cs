// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.Graph;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeGraphClient : IGraphClient
    {
        public Task<AzureApplication> CreateAppAsync(string displayName, CancellationToken ct)
        {
            return Task.FromResult(new AzureApplication
            {
                Id = "FakeId",
                DisplayName = displayName,
                AppId = "FakeAppId",
            });
        }

        public Task<CreateAppSecretResponse> CreateAppSecretAsync(string clientId, string displayName, CancellationToken ct)
        {
            return Task.FromResult(new CreateAppSecretResponse
            {
                SecretText = $"{clientId}FakeSecret",
                DisplayName = displayName,
            });
        }

        public Task<CreatePrincipalResponse> CreatePrincipalAsync(string appId, CancellationToken ct)
        {
            return Task.FromResult(new CreatePrincipalResponse
            {
                Id = "Id",
                AppId = appId
            });
        }

        public Task<ListResponse<AzureApplication>> GetAppsByDisplayNameAsync(string displayName, CancellationToken ct)
        {
            return Task.FromResult(new ListResponse<AzureApplication>
            {
                Value = new()
            });
        }

        public Task<ListResponse<Organization>> GetOrganizationsAsync(CancellationToken ct)
        {
            return Task.FromResult(new ListResponse<Organization>
            {
                Value = new()
                {
                    new Organization
                    {
                        Id = "F3C1CCB6-09C0-4BAB-BABA-C034BFB60EF9"
                    }
                }
            });
        }

        public Task<string> UpdateAppAsync(string id, AppUpdateRequest updatedApp, CancellationToken ct)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
