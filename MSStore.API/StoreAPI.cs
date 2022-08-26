// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API.Models;

namespace MSStore.API
{
    public class StoreAPI : IStoreAPI, IDisposable
    {
        private static readonly string Version = "1";
        private static readonly string PackagesUrlTemplate = "/submission/v{0}/product/{1}/packages";
        /* private static readonly string PackageByIdUrlTemplate = "/submission/v{0}/product/{1}/packages/{2}"; */
        private static readonly string PackagesCommitUrlTemplate = "/submission/v{0}/product/{1}/packages/commit";
        private static readonly string AppMetadataUrlTemplate = "/submission/v{0}/product/{1}/metadata";
        private static readonly string AppModuleFetchMetadataUrlTemplate = "/submission/v{0}/product/{1}/metadata/{2}?languages={3}";
        private static readonly string ListingAssetsUrlTemplate = "/submission/v{0}/product/{1}/listings/assets?languages={2}";
        /* private static readonly string ListingAssetsCreateUrlTemplate = "/submission/v{0}/product/{1}/listings/assets/create";
        private static readonly string ListingAssetsCommitUrlTemplate = "/submission/v{0}/product/{1}/listings/assets/commit"; */
        private static readonly string ProductDraftStatusPollingUrlTemplate = "/submission/v{0}/product/{1}/status";
        private static readonly string CreateSubmissionUrlTemplate = "/submission/v{0}/product/{1}/submit";
        private static readonly string SubmissionStatusPollingUrlTemplate = "/submission/v{0}/product/{1}/submission/{2}/status";

        private SubmissionClient? _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="StoreAPI"/> class.
        /// </summary>
        /// <param name="configurations">An instance of ClientConfiguration that contains all parameters populated</param>
        /// <param name="clientSecret">The client secret of the Azure AD Application that is registered to call Store APIs</param>
        /// <param name="serviceUrl">The Store API URL used to make the API calls.</param>
        /// <param name="scope">The Scope from the Store APIs that will be used to request the access token.</param>
        /// <param name="logger">ILogger for logs.</param>
        public StoreAPI(
            StoreConfigurations configurations,
            string clientSecret,
            string? serviceUrl,
            string? scope,
            ILogger? logger = null)
        {
            if (configurations.SellerId == null)
            {
                throw new ArgumentNullException(nameof(configurations), "SellerId is required");
            }

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
            ServiceUrl = string.IsNullOrEmpty(serviceUrl) ? "https://api.store.microsoft.com" : serviceUrl;
            Scope = string.IsNullOrEmpty(scope) ? "https://api.store.microsoft.com/.default" : scope;
            Logger = logger;
        }

        private ILogger? Logger { get; }

        public string ClientSecret { get; }
        public string ServiceUrl { get; set; }
        public string Scope { get; set; }

        public StoreConfigurations Config { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        public async Task InitAsync(CancellationToken ct)
        {
            // Get authorization token.
            Logger?.LogInformation("Getting authorization token");
            var accessToken = await SubmissionClient.GetClientCredentialAccessTokenAsync(
                Config.TenantId!.Value.ToString(),
                Config.ClientId!.Value.ToString(),
                ClientSecret,
                Scope,
                Logger,
                ct);

            if (string.IsNullOrEmpty(accessToken.AccessToken))
            {
                Logger?.LogError("Access Token should not be null");
                return;
            }

            _client = new SubmissionClient(accessToken, ServiceUrl)
            {
                DefaultHeaders = new Dictionary<string, string>()
                {
                    { "X-Seller-Account-Id", Config.SellerId!.Value.ToString(CultureInfo.InvariantCulture) }
                }
            };
        }

        public async Task<object> GetDraftAsync(string productId, string? moduleName, string languages, CancellationToken ct)
        {
            moduleName = moduleName?.ToLower(CultureInfo.InvariantCulture);
            ResponseWrapper<object> draftResponse = moduleName switch
            {
                "availability" => await GetDraftSubmissionAvailabilityMetadataAsync(productId, languages, ct),
                "listings" => await GetDraftSubmissionListingsMetadataAsync(productId, languages, ct),
                "properties" => await GetDraftSubmissionPropertiesMetadataAsync(productId, languages, ct),
                null => await GetDraftSubmissionPackagesDataAsync(productId, ct),
                _ => throw new ArgumentException("Module name must be 'availability', 'listings' or 'properties'", nameof(moduleName)),
            };
            if (!draftResponse.IsSuccess || draftResponse.ResponseData == null)
            {
                throw new MSStoreWrappedErrorException("Failed to get the draft.", draftResponse.Errors);
            }
            else
            {
                return draftResponse.ResponseData;
            }
        }

        public async Task<UpdateMetadataResponse> UpdateSubmissionMetadataAsync(string productId, UpdateMetadataRequest submissionMetadata, bool skipInitialPolling = false, CancellationToken ct = default)
        {
            if (!skipInitialPolling && !await PollModuleStatusAsync(productId, ct))
            {
                // Wait until all modules are in the ready state
                throw new MSStoreException("Failed to poll module status.");
            }

            Debug.WriteLine(JsonSerializer.Serialize(submissionMetadata, submissionMetadata.GetType(), SourceGenerationContext.GetCustom(true)));

            var updateSubmissionData = await UpdateDraftSubmissionMetadataAsync(productId, submissionMetadata, ct);
            Debug.WriteLine(JsonSerializer.Serialize(updateSubmissionData, updateSubmissionData.GetType(), SourceGenerationContext.GetCustom(true)));

            if (!updateSubmissionData.IsSuccess || updateSubmissionData.ResponseData == null)
            {
                throw new MSStoreWrappedErrorException("Failed to update submission metadata.", updateSubmissionData.Errors);
            }

            if (!await PollModuleStatusAsync(productId, ct))
            {
                // Wait until all modules are in the ready state
                throw new MSStoreException("Failed to poll module status.");
            }

            return updateSubmissionData.ResponseData;
        }

        public async Task<UpdateMetadataResponse> UpdateProductPackagesAsync(string productId, UpdatePackagesRequest updatedProductPackages, bool skipInitialPolling = false, CancellationToken ct = default)
        {
            if (!skipInitialPolling && !await PollModuleStatusAsync(productId, ct))
            {
                // Wait until all modules are in the ready state
                throw new MSStoreException("Failed to poll module status.");
            }

            Debug.WriteLine(updatedProductPackages);

            var updateSubmissionData = await UpdateStoreSubmissionPackagesAsync(productId, updatedProductPackages, ct);
            Debug.WriteLine(JsonSerializer.Serialize(updateSubmissionData, updateSubmissionData.GetType(), SourceGenerationContext.GetCustom(true)));

            if (!updateSubmissionData.IsSuccess || updateSubmissionData.ResponseData == null)
            {
                throw new MSStoreWrappedErrorException("Failed to update submission.", updateSubmissionData.Errors);
            }

            Debug.WriteLine("Committing package changes...");

            var commitResult = await CommitUpdateStoreSubmissionPackagesAsync(productId, ct);
            if (!commitResult.IsSuccess)
            {
                throw new MSStoreWrappedErrorException("Failed to commit the updated submission.", commitResult.Errors);
            }

            Debug.WriteLine(JsonSerializer.Serialize(commitResult, commitResult.GetType(), SourceGenerationContext.GetCustom(true)));

            if (!await PollModuleStatusAsync(productId, ct))
            {
                // Wait until all modules are in the ready state
                throw new MSStoreException("Failed to poll module status.");
            }

            return updateSubmissionData.ResponseData;
        }

        public async Task<string> PublishSubmissionAsync(string productId, CancellationToken ct)
        {
            var commitResult = await CommitUpdateStoreSubmissionPackagesAsync(productId, ct);
            if (!commitResult.IsSuccess)
            {
                throw new MSStoreWrappedErrorException("Failed to commit the updated submission.", commitResult.Errors);
            }

            Debug.WriteLine(JsonSerializer.Serialize(commitResult, commitResult.GetType(), SourceGenerationContext.GetCustom(true)));

            if (!await PollModuleStatusAsync(productId, ct))
            {
                // Wait until all modules are in the ready state
                throw new MSStoreException("Failed to poll module status.");
            }

            string? submissionId = null;

            var submitSubmissionResponse = await SubmitSubmissionAsync(productId, ct);
            Debug.WriteLine(JsonSerializer.Serialize(submitSubmissionResponse, submitSubmissionResponse.GetType(), SourceGenerationContext.GetCustom(true)));
            if (submitSubmissionResponse.IsSuccess && submitSubmissionResponse.ResponseData != null)
            {
                if (submitSubmissionResponse.ResponseData.SubmissionId != null &&
                    submitSubmissionResponse.ResponseData.SubmissionId.Length > 0)
                {
                    submissionId = submitSubmissionResponse.ResponseData.SubmissionId;
                }
                else if (
                  submitSubmissionResponse.ResponseData.OngoingSubmissionId != null &&
                  submitSubmissionResponse.ResponseData.OngoingSubmissionId.Length > 0
                )
                {
                    submissionId =
                      submitSubmissionResponse.ResponseData.OngoingSubmissionId;
                }
            }

            if (submissionId == null)
            {
                Debug.WriteLine("Failed to get submission ID");
                throw new MSStoreException("Failed to get submission ID");
            }
            else
            {
                return submissionId;
            }
        }

        public async Task<ListingAssetsResponse> GetDraftListingAssetsAsync(string productId, string languages, CancellationToken ct)
        {
            try
            {
                var draftListingAssetsResponse = await InternalGetDraftListingAssetsAsync(productId, languages, ct);
                if (!draftListingAssetsResponse.IsSuccess || draftListingAssetsResponse.ResponseData == null)
                {
                    throw new MSStoreWrappedErrorException("Failed to get the draft listing assets.", draftListingAssetsResponse.Errors);
                }
                else
                {
                    return draftListingAssetsResponse.ResponseData;
                }
            }
            catch (Exception error)
            {
                throw new MSStoreException($"Failed to get the draft listing assets - {error.Message}", error);
            }
        }

        [MemberNotNull(nameof(_client))]
        private void AssertClientInitialized()
        {
            if (_client == null)
            {
                throw new MSStoreException("Client is not initialized");
            }
        }

        private async Task<ResponseWrapper<PropertiesMetadataResponse>> GetDraftSubmissionPropertiesMetadataAsync(string productId, string languages, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<PropertiesMetadataResponse>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    AppModuleFetchMetadataUrlTemplate,
                    Version,
                    productId,
                    "properties",
                    languages),
                null,
                ct);
        }

        private async Task<ResponseWrapper<AvailabilityMetadataResponse>> GetDraftSubmissionAvailabilityMetadataAsync(string productId, string languages, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<AvailabilityMetadataResponse>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    AppModuleFetchMetadataUrlTemplate,
                    Version,
                    productId,
                    "availability",
                    languages),
                null,
                ct);
        }

        private async Task<ResponseWrapper<ListingsMetadataResponse>> GetDraftSubmissionListingsMetadataAsync(string productId, string languages, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<ListingsMetadataResponse>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    AppModuleFetchMetadataUrlTemplate,
                    Version,
                    productId,
                    "listings",
                    languages),
                null,
                ct);
        }

        private async Task<ResponseWrapper<PackagesMetadataResponse>> GetDraftSubmissionPackagesDataAsync(string productId, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<PackagesMetadataResponse>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    PackagesUrlTemplate,
                    Version,
                    productId),
                null,
                ct);
        }

        private async Task<ResponseWrapper<UpdateMetadataResponse>> UpdateDraftSubmissionMetadataAsync(string productId, UpdateMetadataRequest submissionMetadata, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<UpdateMetadataResponse>>(
                HttpMethod.Put,
                string.Format(
                    CultureInfo.InvariantCulture,
                    AppMetadataUrlTemplate,
                    Version,
                    productId),
                submissionMetadata,
                ct);
        }

        private async Task<ResponseWrapper<UpdateMetadataResponse>> UpdateStoreSubmissionPackagesAsync(string productId, UpdatePackagesRequest submission, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<UpdateMetadataResponse>>(
                HttpMethod.Put,
                string.Format(
                    CultureInfo.InvariantCulture,
                    PackagesUrlTemplate,
                    Version,
                    productId),
                submission,
                ct);
        }

        private async Task<ResponseWrapper<UpdateMetadataResponse>> CommitUpdateStoreSubmissionPackagesAsync(string productId, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<UpdateMetadataResponse>>(
                HttpMethod.Post,
                string.Format(
                    CultureInfo.InvariantCulture,
                    PackagesCommitUrlTemplate,
                    Version,
                    productId),
                null,
                ct);
        }

        public async Task<ResponseWrapper<ModuleStatus>> GetModuleStatusAsync(string productId, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<ModuleStatus>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    ProductDraftStatusPollingUrlTemplate,
                    Version,
                    productId),
                null,
                ct);
        }

        public async Task<ResponseWrapper<SubmissionStatus>> GetSubmissionStatusPollingAsync(string productId, string submissionId, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<SubmissionStatus>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    SubmissionStatusPollingUrlTemplate,
                    Version,
                    productId,
                    submissionId),
                null,
                ct);
        }

        private async Task<ResponseWrapper<CreateSubmissionResponse>> SubmitSubmissionAsync(string productId, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<CreateSubmissionResponse>>(
                HttpMethod.Post,
                string.Format(
                    CultureInfo.InvariantCulture,
                    CreateSubmissionUrlTemplate,
                    Version,
                    productId),
                null,
                ct);
        }

        private async Task<ResponseWrapper<ListingAssetsResponse>> InternalGetDraftListingAssetsAsync(string productId, string languages, CancellationToken ct)
        {
            AssertClientInitialized();

            return await _client.InvokeAsync<ResponseWrapper<ListingAssetsResponse>>(
                HttpMethod.Get,
                string.Format(
                    CultureInfo.InvariantCulture,
                    ListingAssetsUrlTemplate,
                    Version,
                    productId,
                    languages),
                null,
                ct);
        }

        private async Task<bool> PollModuleStatusAsync(string productId, CancellationToken ct)
        {
            ModuleStatus? status = new()
            {
                IsReady = false
            };

            while (status != null && !status.IsReady)
            {
                var moduleStatus = await GetModuleStatusAsync(productId, ct);
                Debug.WriteLine(JsonSerializer.Serialize(moduleStatus, moduleStatus.GetType(), SourceGenerationContext.GetCustom(true)));
                status = moduleStatus.ResponseData;

                if (moduleStatus.Errors != null &&
                      moduleStatus.Errors.Count > 0 &&
                      moduleStatus.Errors.Any((e) => e.Target != "packages" || e.Code == "packageuploaderror"))
                {
                    Debug.WriteLine(moduleStatus.Errors);
                    return false;
                }

                if (!moduleStatus.IsSuccess)
                {
                    // TODO
                    /*
                    var errorResponse = moduleStatus as ErrorResponse;
                    if (errorResponse.StatusCode == 401)
                    {
                        Debug.WriteLine($"Access token expired. Requesting new one. (message='{errorResponse.Message}')");
                        await InitAsync(ct);
                        status = new ModuleStatus
                        {
                            IsReady = false
                        };
                        continue;
                    }
                    */

                    Debug.WriteLine("Error");
                    break;
                }

                if (status?.IsReady == true)
                {
                    Debug.WriteLine("Success!");
                    return true;
                }

                Debug.WriteLine("Waiting 10 seconds.");
                await Task.Delay(10000, ct);
            }

            return false;
        }
    }
}
