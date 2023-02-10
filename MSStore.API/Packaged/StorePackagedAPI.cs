// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API.Models;
using MSStore.API.Packaged.Models;

namespace MSStore.API.Packaged
{
    public class StorePackagedAPI : IStorePackagedAPI, IDisposable
    {
        private static readonly string DevCenterVersion = "1.0";
        private static readonly string DevCenterApplicationsTemplate = "/v{0}/my/applications?skip={1}&top={2}";
        private static readonly string DevCenterApplicationTemplate = "/v{0}/my/applications/{1}";
        private static readonly string DevCenterGetSubmissionTemplate = "/v{0}/my/applications/{1}/submissions/{2}";
        private static readonly string DevCenterPutSubmissionTemplate = "/v{0}/my/applications/{1}/submissions/{2}";
        private static readonly string DevCenterCreateSubmissionTemplate = "/v{0}/my/applications/{1}/submissions?isMinimalResponse=true";
        private static readonly string DevCenterDeleteSubmissionTemplate = "/v{0}/my/applications/{1}/submissions/{2}";
        private static readonly string DevCenterCommitSubmissionTemplate = "/v{0}/my/applications/{1}/submissions/{2}/Commit";
        private static readonly string DevCenterSubmissionStatusTemplate = "/v{0}/my/applications/{1}/submissions/{2}/status";

        private SubmissionClient? _devCenterClient;

        public static TimeSpan DefaultSubmissionPollDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="StorePackagedAPI"/> class.
        /// </summary>
        /// <param name="configurations">An instance of ClientConfiguration that contains all parameters populated</param>
        /// <param name="clientSecret">The client secret of the Azure AD Application that is registered to call Store APIs</param>
        /// <param name="devCenterUrl">The DevCenter URL used to make the API calls.</param>
        /// <param name="devCenterScope">The Scope from DevCenter that will be used to request the access token.</param>
        /// <param name="logger">ILogger for logs.</param>
        public StorePackagedAPI(
            StoreConfigurations configurations,
            string clientSecret,
            string? devCenterUrl,
            string? devCenterScope,
            ILogger? logger = null)
        {
            if (configurations.ClientId == null)
            {
                throw new ArgumentNullException(nameof(configurations), "ClientId is required");
            }

            if (configurations.TenantId == null)
            {
                throw new ArgumentNullException(nameof(configurations), "TenantId is required");
            }

            Config = configurations;
            ClientSecret = clientSecret;
            DevCenterUrl = string.IsNullOrEmpty(devCenterUrl) ? "https://manage.devcenter.microsoft.com" : devCenterUrl;
            DevCenterScope = string.IsNullOrEmpty(devCenterScope) ? "https://manage.devcenter.microsoft.com/.default" : devCenterScope;
            Logger = logger;
        }

        private ILogger? Logger { get; }

        public string ClientSecret { get; }
        public string DevCenterUrl { get; set; }
        public string DevCenterScope { get; set; }

        public StoreConfigurations Config { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_devCenterClient != null)
            {
                _devCenterClient.Dispose();
                _devCenterClient = null;
            }
        }

        public async Task InitAsync(HttpClient? httpClient = null, CancellationToken ct = default)
        {
            // Get authorization token.
            Logger?.LogInformation("Getting DevCenter authorization token");
            var devCenterAccessToken = await SubmissionClient.GetClientCredentialAccessTokenAsync(
                Config.TenantId!.Value.ToString(),
                Config.ClientId!.Value.ToString(),
                ClientSecret,
                DevCenterScope,
                Logger,
                ct);

            if (string.IsNullOrEmpty(devCenterAccessToken.AccessToken))
            {
                Logger?.LogError("DevCenter Access Token should not be null");
                return;
            }

            _devCenterClient = new SubmissionClient(devCenterAccessToken, DevCenterUrl, httpClient)
            {
                DefaultHeaders = new Dictionary<string, string>()
                {
                    { "TenantId", Config.TenantId!.Value.ToString() }
                }
            };
        }

        public async Task<List<DevCenterApplication>> GetApplicationsAsync(CancellationToken ct = default)
        {
            try
            {
                var devCenterApplicationsResponse = await GetDevCenterApplicationsAsync(0, 100, ct); // TODO: pagination
                return devCenterApplicationsResponse.Value ?? new();
            }
            catch (Exception error)
            {
                throw new MSStoreException($"Failed to get the applications - {error.Message}", error);
            }
        }

        [MemberNotNull(nameof(_devCenterClient))]
        private void AssertClientInitialized()
        {
            if (_devCenterClient == null)
            {
                throw new MSStoreException("DevCenterClient is not initialized");
            }
        }

        private async Task<PagedResponse<DevCenterApplication>> GetDevCenterApplicationsAsync(int skip = 0, int top = 10, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<PagedResponse<DevCenterApplication>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterApplicationsTemplate,
                    DevCenterVersion,
                    skip,
                    top),
                null,
                ct);
        }

        public async Task<DevCenterError?> DeleteSubmissionAsync(string productId, string submissionId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            var ret = await _devCenterClient.InvokeAsync<string>(
                HttpMethod.Delete,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterDeleteSubmissionTemplate,
                    DevCenterVersion,
                    productId,
                    submissionId),
                null,
                ct);

            if (string.IsNullOrEmpty(ret))
            {
                return null;
            }

            return JsonSerializer.Deserialize(ret, typeof(DevCenterError), SourceGenerationContext.GetCustom()) as DevCenterError;
        }

        public async Task<DevCenterApplication> GetApplicationAsync(string productId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterApplication>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterApplicationTemplate,
                    DevCenterVersion,
                    productId),
                null,
                ct);
        }

        public async Task<DevCenterSubmission> CreateSubmissionAsync(string productId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterSubmission>(
                HttpMethod.Post,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterCreateSubmissionTemplate,
                    DevCenterVersion,
                    productId),
                null,
                ct);
        }

        public async Task<DevCenterSubmission> GetSubmissionAsync(string productId, string submissionId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterSubmission>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterGetSubmissionTemplate,
                    DevCenterVersion,
                    productId,
                    submissionId),
                null,
                ct);
        }

        public async Task<DevCenterSubmission> UpdateSubmissionAsync(string productId, string submissionId, DevCenterSubmission updatedSubmission, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterSubmission>(
                HttpMethod.Put,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterPutSubmissionTemplate,
                    DevCenterVersion,
                    productId,
                    submissionId),
                updatedSubmission,
                ct);
        }

        public async Task<DevCenterCommitResponse?> CommitSubmissionAsync(string productId, string submissionId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            var ret = await _devCenterClient.InvokeAsync<string>(
                    HttpMethod.Post,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        DevCenterCommitSubmissionTemplate,
                        DevCenterVersion,
                        productId,
                        submissionId),
                    null,
                    ct);

            return JsonSerializer.Deserialize(ret, typeof(DevCenterCommitResponse), SourceGenerationContext.GetCustom()) as DevCenterCommitResponse;
        }

        public async Task<DevCenterSubmissionStatusResponse> GetSubmissionStatusAsync(string productId, string submissionId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterSubmissionStatusResponse>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterSubmissionStatusTemplate,
                    DevCenterVersion,
                    productId,
                    submissionId),
                null,
                ct);
        }
    }
}
