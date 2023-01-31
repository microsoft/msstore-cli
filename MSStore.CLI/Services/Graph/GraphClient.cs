// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API;
using MSStore.CLI.Services.TokenManager;

namespace MSStore.CLI.Services.Graph
{
    internal class GraphClient : IGraphClient, IDisposable
    {
        internal static readonly string[] GraphApplicationReadWriteScope = new[] { "https://graph.microsoft.com/Application.ReadWrite.All" };

        private static readonly string JsonContentType = "application/json";

        private readonly HttpClient httpClient;

        private readonly ITokenManager _tokenManager;

        public bool Enabled => true;

        public GraphClient(ITokenManager tokenManager)
        {
            httpClient = new HttpClient(
                new HttpClientHandler
                {
                    CheckCertificateRevocationList = true
                })
            {
                BaseAddress = new Uri("https://graph.microsoft.com/")
            };

            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                httpClient?.Dispose();
            }
        }

        private async Task<T> InvokeAsync<T>(
            HttpMethod httpMethod,
            string relativeUrl,
            object? requestContent,
            string[] scopes,
            Dictionary<string, string>? extraHeaders,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(httpMethod, relativeUrl);

            await SetRequestAsync(request, requestContent, extraHeaders, scopes, ct);

            ct.ThrowIfCancellationRequested();

            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);

            if (typeof(T) == typeof(string))
            {
                return (T)(object)await response.Content.ReadAsStringAsync(ct);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new MSStoreException(await response.Content.ReadAsStringAsync(ct));
            }

            var resource = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(ct), typeof(T), GraphSourceGenerationContext.GetCustom());
            if (resource is T result)
            {
                return result;
            }
            else
            {
                throw new MSStoreException(await response.Content.ReadAsStringAsync(ct));
            }
        }

        private async Task SetRequestAsync(HttpRequestMessage request, object? requestContent, Dictionary<string, string>? extraHeaders, string[] scopes, CancellationToken ct)
        {
            var authenticationResult = await _tokenManager.GetTokenAsync(scopes, ct);

            if (authenticationResult == null)
            {
                throw new InvalidOperationException("Could not retrieve access token.");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);

            if (extraHeaders != null)
            {
                foreach (var header in extraHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            if (requestContent != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestContent, requestContent.GetType(), GraphSourceGenerationContext.GetCustom()),
                    Encoding.UTF8,
                    JsonContentType);
            }
            else
            {
                request.Content = new StringContent(string.Empty, Encoding.UTF8, JsonContentType);
            }
        }

        public async Task<AzureApplication> CreateAppAsync(string displayName, CancellationToken ct)
        {
            return await InvokeAsync<AzureApplication>(
                HttpMethod.Post,
                "v1.0/applications",
                new AppRegistrationRequest
                {
                    DisplayName = displayName
                },
                GraphApplicationReadWriteScope,
                null,
                ct);
        }

        public async Task<ListResponse<AzureApplication>> GetAppsByDisplayNameAsync(string displayName, CancellationToken ct)
        {
            return await InvokeAsync<ListResponse<AzureApplication>>(
                HttpMethod.Get,
                $"v1.0/applications?$filter=startswith(displayName, '{displayName}')&$count=true&$top=1&$orderby=displayName",
                null,
                GraphApplicationReadWriteScope,
                new Dictionary<string, string>()
                {
                    { "ConsistencyLevel", "eventual" }
                },
                ct);
        }

        public async Task<CreatePrincipalResponse> CreatePrincipalAsync(string appId, CancellationToken ct)
        {
            return await InvokeAsync<CreatePrincipalResponse>(
                HttpMethod.Post,
                "v1.0/servicePrincipals",
                new CreatePrincipalRequest
                {
                    AppId = appId
                },
                GraphApplicationReadWriteScope,
                null,
                ct);
        }

        public async Task<string> UpdateAppAsync(string id, AppUpdateRequest updatedApp, CancellationToken ct)
        {
            return await InvokeAsync<string>(
                HttpMethod.Patch,
                $"v1.0/applications/{id}",
                updatedApp,
                GraphApplicationReadWriteScope,
                null,
                ct);
        }

        public async Task<CreateAppSecretResponse> CreateAppSecretAsync(string clientId, string displayName, CancellationToken ct)
        {
            return await InvokeAsync<CreateAppSecretResponse>(
                HttpMethod.Post,
                $"v1.0/applications/{clientId}/addPassword",
                new AppPasswordRegistrationRequest
                {
                    DisplayName = displayName,
                    StartDateTime = "now",
                    EndDateTime = "startDateTime + 2 years"
                },
                GraphApplicationReadWriteScope,
                null,
                ct);
        }
    }
}
