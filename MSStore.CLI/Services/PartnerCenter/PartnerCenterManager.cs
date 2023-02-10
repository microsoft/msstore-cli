// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API;
using MSStore.CLI.Services.TokenManager;

namespace MSStore.CLI.Services.PartnerCenter
{
    internal class PartnerCenterManager : IPartnerCenterManager
    {
        private static readonly string JsonContentType = "application/json";

        private static readonly string[] PartnerCenterUserImpersonationScope = new[] { "https://api.partnercenter.microsoft.com/user_impersonation" };

        private readonly ITokenManager _tokenManager;
        private readonly IHttpClientFactory _httpClientFactory;

        public bool Enabled => false;

        public PartnerCenterManager(ITokenManager tokenManager, IHttpClientFactory httpClientFactory)
        {
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        private async Task<T> InvokeAsync<T>(
            HttpMethod httpMethod,
            string relativeUrl,
            object? requestContent,
            string[] scopes,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(httpMethod, relativeUrl);

            await SetRequestAsync(request, requestContent, scopes, ct);

            ct.ThrowIfCancellationRequested();

            using var httpClient = _httpClientFactory.CreateClient(nameof(PartnerCenterManager));

            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);

            if (typeof(T) == typeof(string))
            {
                return (T)(object)await response.Content.ReadAsStringAsync(ct);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new MSStoreException(await response.Content.ReadAsStringAsync(ct));
            }

            var resource = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(ct), typeof(T), PartnerCenterGenerationContext.GetCustom());
            if (resource is T result)
            {
                return result;
            }
            else
            {
                throw new MSStoreException(await response.Content.ReadAsStringAsync(ct));
            }
        }

        private async Task SetRequestAsync(HttpRequestMessage request, object? requestContent, string[] scopes, CancellationToken ct)
        {
            var authenticationResult = await _tokenManager.GetTokenAsync(scopes, ct);

            if (authenticationResult == null)
            {
                throw new InvalidOperationException("Could not retrieve access token.");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);

            if (requestContent != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestContent, requestContent.GetType(), PartnerCenterGenerationContext.GetCustom()),
                    Encoding.UTF8,
                    JsonContentType);
            }
            else
            {
                request.Content = new StringContent(string.Empty, Encoding.UTF8, JsonContentType);
            }
        }

        public async Task<AccountEnrollments> GetEnrollmentAccountsAsync(CancellationToken ct)
        {
            return await InvokeAsync<AccountEnrollments>(
                HttpMethod.Get,
                $"accountenrollments/v1/accounts?basicInfoOnly=true",
                null,
                PartnerCenterUserImpersonationScope,
                ct);
        }
    }
}
