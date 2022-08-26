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
    internal class PartnerCenterManager : IPartnerCenterManager, IDisposable
    {
        private static readonly string JsonContentType = "application/json";

        private static readonly string[] PartnerCenterUserImpersonationScope = new[] { "https://api.partnercenter.microsoft.com/user_impersonation" };

        private readonly HttpClient _httpClient;

        private readonly ITokenManager _tokenManager;

        public bool Enabled => false;

        public PartnerCenterManager(ITokenManager tokenManager)
        {
            _httpClient = new HttpClient(
                new HttpClientHandler
                {
                    CheckCertificateRevocationList = true
                })
            {
                BaseAddress = new Uri("https://api.partnercenter.microsoft.com")
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
                _httpClient?.Dispose();
            }
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

            using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);

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
