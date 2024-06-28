// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        private static readonly CompositeFormat DevCenterApplicationsTemplate = CompositeFormat.Parse("/v{0}/my/applications?skip={1}&top={2}");
        private static readonly CompositeFormat DevCenterApplicationTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}");
        private static readonly CompositeFormat DevCenterGetSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/submissions/{2}");
        private static readonly CompositeFormat DevCenterPutSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/submissions/{2}");
        private static readonly CompositeFormat DevCenterCreateSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/submissions?isMinimalResponse=true");
        private static readonly CompositeFormat DevCenterDeleteSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/submissions/{2}");
        private static readonly CompositeFormat DevCenterCommitSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/submissions/{2}/Commit");
        private static readonly CompositeFormat DevCenterSubmissionStatusTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/submissions/{2}/status");
        private static readonly CompositeFormat DevCenterListFlightsTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/listflights?skip={2}&top={3}");
        private static readonly CompositeFormat DevCenterGetFlightTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/flights/{2}");
        private static readonly CompositeFormat DevCenterCreateFlightSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/flights/{2}/submissions");
        private static readonly CompositeFormat DevCenterPutFlightSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/flights/{2}/submissions/{3}");
        private static readonly CompositeFormat DevCenterGetFlightSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/flights/{2}/submissions/{3}");
        private static readonly CompositeFormat DevCenterDeleteFlightSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/flights/{2}/submissions/{3}");
        private static readonly CompositeFormat DevCenterCommitFlightSubmissionTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/flights/{2}/submissions/{3}/commit");
        private static readonly CompositeFormat DevCenterFlightSubmissionStatusTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}/flights/{2}/submissions/{3}/status");
        private static readonly CompositeFormat DevCenterGetPackageRolloutTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}{2}/submissions/{3}/packagerollout");
        private static readonly CompositeFormat DevCenterUpdatePackageRolloutPercentageTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}{2}/submissions/{3}/updatepackagerolloutpercentage?percentage={4}");
        private static readonly CompositeFormat DevCenterHaltPackageRolloutTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}{2}/submissions/{3}/haltpackagerollout");
        private static readonly CompositeFormat DevCenterFinalizePackageRolloutTemplate = CompositeFormat.Parse("/v{0}/my/applications/{1}{2}/submissions/{3}/finalizepackagerollout");

        private SubmissionClient? _devCenterClient;

        public static TimeSpan DefaultSubmissionPollDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="StorePackagedAPI"/> class.
        /// </summary>
        /// <param name="configurations">An instance of ClientConfiguration that contains all parameters populated</param>
        /// <param name="clientSecret">The client secret of the Microsoft Entra Application that is registered to call Store APIs</param>
        /// <param name="devCenterUrl">The DevCenter URL used to make the API calls.</param>
        /// <param name="devCenterScope">The Scope from DevCenter that will be used to request the access token.</param>
        /// <param name="logger">ILogger for logs.</param>
        public StorePackagedAPI(
            StoreConfigurations configurations,
            string clientSecret,
            string? devCenterUrl,
            string? devCenterScope,
            ILogger? logger = null)
            : this(configurations, devCenterUrl, devCenterScope, logger)
        {
            ClientSecret = clientSecret;
            Certificate = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorePackagedAPI"/> class.
        /// </summary>
        /// <param name="configurations">An instance of ClientConfiguration that contains all parameters populated</param>
        /// <param name="certificate">The client certificate of the Microsoft Entra Application that is registered to call Store APIs</param>
        /// <param name="devCenterUrl">The DevCenter URL used to make the API calls.</param>
        /// <param name="devCenterScope">The Scope from DevCenter that will be used to request the access token.</param>
        /// <param name="logger">ILogger for logs.</param>
        public StorePackagedAPI(
            StoreConfigurations configurations,
            X509Certificate2 certificate,
            string? devCenterUrl,
            string? devCenterScope,
            ILogger? logger = null)
            : this(configurations, devCenterUrl, devCenterScope, logger)
        {
            ClientSecret = null;
            Certificate = certificate;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorePackagedAPI"/> class.
        /// </summary>
        /// <param name="configurations">An instance of ClientConfiguration that contains all parameters populated</param>
        /// <param name="devCenterUrl">The DevCenter URL used to make the API calls.</param>
        /// <param name="devCenterScope">The Scope from DevCenter that will be used to request the access token.</param>
        /// <param name="logger">ILogger for logs.</param>
        private StorePackagedAPI(
            StoreConfigurations configurations,
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
            DevCenterUrl = string.IsNullOrEmpty(devCenterUrl) ? "https://manage.devcenter.microsoft.com" : devCenterUrl;
            DevCenterScope = string.IsNullOrEmpty(devCenterScope) ? "https://manage.devcenter.microsoft.com/.default" : devCenterScope;
            Logger = logger;
        }

        private ILogger? Logger { get; }

        public string? ClientSecret { get; }
        public X509Certificate2? Certificate { get; }
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
            Microsoft.Identity.Client.AuthenticationResult? devCenterAccessToken = null;
            if (Certificate != null)
            {
                devCenterAccessToken = await SubmissionClient.GetClientCredentialAccessTokenAsync(
                    Config.TenantId!.Value.ToString(),
                    Config.ClientId!.Value.ToString(),
                    Certificate,
                    DevCenterScope,
                    Logger,
                    ct);
            }
            else if (ClientSecret != null)
            {
                devCenterAccessToken = await SubmissionClient.GetClientCredentialAccessTokenAsync(
                    Config.TenantId!.Value.ToString(),
                    Config.ClientId!.Value.ToString(),
                    ClientSecret,
                    DevCenterScope,
                    Logger,
                    ct);
            }

            if (string.IsNullOrEmpty(devCenterAccessToken?.AccessToken))
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

        public async Task<List<DevCenterFlight>> GetFlightsAsync(string productId, CancellationToken ct = default)
        {
            try
            {
                var devCenterFlightsResponse = await GetFlightsAsync(productId, 0, 100, ct); // TODO: pagination
                return devCenterFlightsResponse.Value ?? new();
            }
            catch (Exception error)
            {
                throw new MSStoreException($"Failed to get the flights - {error.Message}", error);
            }
        }

        private async Task<PagedResponse<DevCenterFlight>> GetFlightsAsync(string productId, int skip = 0, int top = 10, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<PagedResponse<DevCenterFlight>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterListFlightsTemplate,
                    DevCenterVersion,
                    productId,
                    skip,
                    top),
                null,
                ct);
        }

        public async Task<DevCenterFlight> GetFlightAsync(string productId, string flightId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterFlight>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterGetFlightTemplate,
                    DevCenterVersion,
                    productId,
                    flightId),
                null,
                ct);
        }

        public async Task<DevCenterFlightSubmission> GetFlightSubmissionAsync(string productId, string flightId, string submissionId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterFlightSubmission>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterGetFlightSubmissionTemplate,
                    DevCenterVersion,
                    productId,
                    flightId,
                    submissionId),
                null,
                ct);
        }

        public async Task<DevCenterError?> DeleteFlightSubmissionAsync(string productId, string flightId, string submissionId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            var ret = await _devCenterClient.InvokeAsync<string>(
                HttpMethod.Delete,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterDeleteFlightSubmissionTemplate,
                    DevCenterVersion,
                    productId,
                    flightId,
                    submissionId),
                null,
                ct);

            if (string.IsNullOrEmpty(ret))
            {
                return null;
            }

            return JsonSerializer.Deserialize(ret, typeof(DevCenterError), SourceGenerationContext.GetCustom()) as DevCenterError;
        }

        public async Task<DevCenterFlightSubmission> CreateFlightSubmissionAsync(string productId, string flightId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterFlightSubmission>(
                HttpMethod.Post,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterCreateFlightSubmissionTemplate,
                    DevCenterVersion,
                    productId,
                    flightId),
                null,
                ct);
        }

        public async Task<DevCenterFlightSubmission> UpdateFlightSubmissionAsync(string productId, string flightId, string submissionId, DevCenterFlightSubmissionUpdate updatedFlightSubmission, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterFlightSubmission>(
                HttpMethod.Put,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterPutFlightSubmissionTemplate,
                    DevCenterVersion,
                    productId,
                    flightId,
                    submissionId),
                updatedFlightSubmission,
                ct);
        }

        public async Task<DevCenterCommitResponse?> CommitFlightSubmissionAsync(string productId, string flightId, string submissionId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            var ret = await _devCenterClient.InvokeAsync<string>(
                    HttpMethod.Post,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        DevCenterCommitFlightSubmissionTemplate,
                        DevCenterVersion,
                        productId,
                        flightId,
                        submissionId),
                    null,
                    ct);

            return JsonSerializer.Deserialize(ret, typeof(DevCenterCommitResponse), SourceGenerationContext.GetCustom()) as DevCenterCommitResponse;
        }

        public async Task<DevCenterSubmissionStatusResponse> GetFlightSubmissionStatusAsync(string productId, string flightId, string submissionId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<DevCenterSubmissionStatusResponse>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterFlightSubmissionStatusTemplate,
                    DevCenterVersion,
                    productId,
                    flightId,
                    submissionId),
                null,
                ct);
        }

        public async Task<PackageRollout> GetPackageRolloutAsync(string productId, string submissionId, string? flightId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<PackageRollout>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterGetPackageRolloutTemplate,
                    DevCenterVersion,
                    productId,
                    flightId == null ? string.Empty : $"/flights/{flightId}",
                    submissionId),
                null,
                ct);
        }

        public async Task<PackageRollout> UpdatePackageRolloutPercentageAsync(string productId, string submissionId, string? flightId, float percentage, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<PackageRollout>(
                HttpMethod.Post,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterUpdatePackageRolloutPercentageTemplate,
                    DevCenterVersion,
                    productId,
                    flightId == null ? string.Empty : $"/flights/{flightId}",
                    submissionId,
                    percentage),
                null,
                ct);
        }

        public async Task<PackageRollout> HaltPackageRolloutAsync(string productId, string submissionId, string? flightId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<PackageRollout>(
                HttpMethod.Post,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterHaltPackageRolloutTemplate,
                    DevCenterVersion,
                    productId,
                    flightId == null ? string.Empty : $"/flights/{flightId}",
                    submissionId),
                null,
                ct);
        }

        public async Task<PackageRollout> FinalizePackageRolloutAsync(string productId, string submissionId, string? flightId, CancellationToken ct = default)
        {
            AssertClientInitialized();

            return await _devCenterClient.InvokeAsync<PackageRollout>(
                HttpMethod.Post,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DevCenterFinalizePackageRolloutTemplate,
                    DevCenterVersion,
                    productId,
                    flightId == null ? string.Empty : $"/flights/{flightId}",
                    submissionId),
                null,
                ct);
        }
    }
}
