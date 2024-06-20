// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using MSStore.API.Models;

namespace MSStore.API
{
    /// <summary>
    /// This class is a proxy that abstracts the functionality of the API service
    /// </summary>
    public class SubmissionClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly bool httpClientIsInternal;

        private readonly AuthenticationResult accessToken;

        protected static readonly string JsonContentType = "application/json";
        protected static readonly string PngContentType = "image/png";
        protected static readonly string BinaryStreamContentType = "application/octet-stream";

        private static readonly string CacheFileName = "msstore_msal_cache.txt";

        private static readonly string KeyChainServiceName = "msstore_msal_service";
        private static readonly string KeyChainAccountName = "msstore_msal_account";

        private static readonly string LinuxKeyRingSchema = "com.microsoft.store.tokencache";
        private static readonly string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
        private static readonly string LinuxKeyRingLabel = "MSAL token cache for all Microsoft Store Apps.";
        private static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Version", "1");
        private static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("ProductGroup", "msstore-api");

        /// <summary>
        /// Initializes a new instance of the <see cref="SubmissionClient" /> class.
        /// </summary>
        /// <param name="accessToken">
        /// The access token. This is JWT a token obtained from Azure Active Directory allowing the caller to invoke the API
        /// on behalf of a user
        /// </param>
        /// <param name="serviceUrl">The service URL.</param>
        /// <param name="httpClient">An HttpClient object to be used in the internal requests. Can be used for mocking.</param>
        public SubmissionClient(AuthenticationResult accessToken, string serviceUrl, HttpClient? httpClient = null)
        {
            if (string.IsNullOrEmpty(accessToken?.AccessToken))
            {
                throw new ArgumentNullException(nameof(accessToken));
            }

            if (string.IsNullOrEmpty(serviceUrl))
            {
                throw new ArgumentNullException(nameof(serviceUrl));
            }

            this.accessToken = accessToken;
            if (httpClient != null)
            {
                this.httpClient = httpClient;
                httpClientIsInternal = false;
            }
            else
            {
                this.httpClient = new HttpClient(
                    new HttpClientHandler
                    {
                        CheckCertificateRevocationList = true
                    });
                httpClientIsInternal = true;
            }

            this.httpClient.BaseAddress = new Uri(serviceUrl);

            DefaultHeaders = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or Sets the default headers.
        /// </summary>
        public Dictionary<string, string> DefaultHeaders { get; set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting
        /// unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (httpClientIsInternal)
                {
                    httpClient?.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets the authorization token for the provided client id, client secret, and the scope.
        /// This token is usually valid for 1 hour, so if your submission takes longer than that to complete,
        /// make sure to get a new one periodically.
        /// </summary>
        /// <param name="tenantId">The tenantId used to get the access token, specific to your
        /// Azure Active Directory app. Example: "d454d300-128e-2d81-334a-27d9b2baf002"</param>
        /// <param name="clientId">Client Id of your Azure Active Directory app. Example: "ba3c223b-03ab-4a44-aa32-38aa10c27e32"</param>
        /// <param name="clientSecret">Client secret of your Azure Active Directory app</param>
        /// <param name="scope">Scope. If not provided, default one is used for the production API endpoint.</param>
        /// <param name="logger">ILogger for logs.</param>
        /// <param name="ct">Cancelation token.</param>
        /// <returns>Autorization token. Prepend it with "Bearer: " and pass it in the request header as the
        /// value for "Authorization: " header.</returns>
        public static Task<AuthenticationResult> GetClientCredentialAccessTokenAsync(
            string tenantId,
            string clientId,
            string clientSecret,
            string scope,
            ILogger? logger = null,
            CancellationToken ct = default)
        {
            return GetClientCredentialAccessTokenAsync(tenantId, clientId, (builder) => builder.WithClientSecret(clientSecret), scope, logger, ct);
        }

        /// <summary>
        /// Gets the authorization token for the provided client id, client secret, and the scope.
        /// This token is usually valid for 1 hour, so if your submission takes longer than that to complete,
        /// make sure to get a new one periodically.
        /// </summary>
        /// <param name="tenantId">The tenantId used to get the access token, specific to your
        /// Azure Active Directory app. Example: "d454d300-128e-2d81-334a-27d9b2baf002"</param>
        /// <param name="clientId">Client Id of your Azure Active Directory app. Example: "ba3c223b-03ab-4a44-aa32-38aa10c27e32"</param>
        /// <param name="certificate">Client certificate of your Azure Active Directory app</param>
        /// <param name="scope">Scope. If not provided, default one is used for the production API endpoint.</param>
        /// <param name="logger">ILogger for logs.</param>
        /// <param name="ct">Cancelation token.</param>
        /// <returns>Autorization token. Prepend it with "Bearer: " and pass it in the request header as the
        /// value for "Authorization: " header.</returns>
        public static Task<AuthenticationResult> GetClientCredentialAccessTokenAsync(
            string tenantId,
            string clientId,
            X509Certificate2 certificate,
            string scope,
            ILogger? logger = null,
            CancellationToken ct = default)
        {
            return GetClientCredentialAccessTokenAsync(tenantId, clientId, (builder) => builder.WithCertificate(certificate, true), scope, logger, ct);
        }

        private static async Task<AuthenticationResult> GetClientCredentialAccessTokenAsync(
            string tenantId,
            string clientId,
            Func<ConfidentialClientApplicationBuilder, ConfidentialClientApplicationBuilder> authorizationBuilder,
            string scope,
            ILogger? logger = null,
            CancellationToken ct = default)
        {
            using ((logger ?? NullLogger.Instance).BeginScope("GetClientCredentialAccessToken"))
            {
                var app = await CreateAppAsync(clientId, authorizationBuilder);

                AuthenticationResult authenticationResult;

                try
                {
                    authenticationResult = await app.AcquireTokenForClient(new[] { scope })
                                           .WithTenantId(tenantId)
                                           .ExecuteAsync(ct);
                }
                catch (Exception e)
                {
                    logger?.LogError(e, "Failed to get access token.");
                    throw new MSStoreException("Could not retrieve access token", e);
                }

                return authenticationResult;
            }
        }

        private static async Task<IConfidentialClientApplication> CreateAppAsync(string clientId, Func<ConfidentialClientApplicationBuilder, ConfidentialClientApplicationBuilder> authorizationBuilder)
        {
            var cacheDirectory = Path.Combine(MsalCacheHelper.UserRootDirectory, "Microsoft", "MSStore.API", "Cache");

            Directory.CreateDirectory(cacheDirectory);

            var storageProperties = new StorageCreationPropertiesBuilder(CacheFileName, cacheDirectory)
                 .WithLinuxKeyring(
                     LinuxKeyRingSchema,
                     LinuxKeyRingCollection,
                     LinuxKeyRingLabel,
                     LinuxKeyRingAttr1,
                     LinuxKeyRingAttr2)
                 .WithMacKeyChain(
                     KeyChainServiceName,
                     KeyChainAccountName)
                 .Build();

            var app = authorizationBuilder(ConfidentialClientApplicationBuilder
                .Create(clientId))
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);

            return app;
        }

        /// <summary>
        /// Invokes the specified HTTP method.
        /// </summary>
        /// <typeparam name="T">The return type expected by the HTTP return.</typeparam>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <param name="relativeUrl">The relative URL.</param>
        /// <param name="requestContent">Content of the request.</param>
        /// <param name="ct">Cancelation token.</param>
        /// <returns>instance of the type T</returns>
        public async Task<T> InvokeAsync<T>(
            HttpMethod httpMethod,
            string relativeUrl,
            object? requestContent,
            CancellationToken ct = default)
            where T : class
        {
            using var request = new HttpRequestMessage(httpMethod, relativeUrl);

            SetRequest(request, requestContent);

            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);

            if (typeof(T) == typeof(string))
            {
                return (T)(object)await response.Content.ReadAsStringAsync(ct);
            }

            object? resource = null;

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    resource = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(ct), typeof(T), SourceGenerationContext.GetCustom());
                }
                catch
                {
                    // ignore
                }
                finally
                {
                }

                if (resource?.GetType()?.IsGenericType == true &&
                    resource?.GetType()?.GetGenericTypeDefinition() == typeof(ResponseWrapper<>))
                {
                    var errorsProp = resource.GetType().GetProperty(nameof(ResponseWrapper<object>.Errors));
                    var isSuccessProp = resource.GetType().GetProperty(nameof(ResponseWrapper<object>.IsSuccess));
                    if (errorsProp != null && isSuccessProp != null)
                    {
                        var errors = (List<ResponseError>?)errorsProp.GetValue(resource);
                        var isSuccess = (bool?)isSuccessProp.GetValue(resource);
                        if (errors?.Count > 0
                            && isSuccess == false)
                        {
                            if (resource is T t)
                            {
                                return t;
                            }

                            throw new MSStoreWrappedErrorException("REST error", errors);
                        }
                    }
                }

                if (response.Content.Headers.ContentLength != 0)
                {
                    throw new MSStoreException(await response.Content.ReadAsStringAsync(ct));
                }
                else
                {
                    throw new MSStoreHttpException(response);
                }
            }

            resource = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(ct), typeof(T), SourceGenerationContext.GetCustom());
            if (resource is T result)
            {
                return result;
            }
            else
            {
                throw new MSStoreException(await response.Content.ReadAsStringAsync(ct));
            }
        }

        /// <summary>
        /// Sets the request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="requestContent">Content of the request.</param>
        protected virtual void SetRequest(HttpRequestMessage request, object? requestContent)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);

            foreach (var header in DefaultHeaders)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            if (requestContent != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestContent, requestContent.GetType(), SourceGenerationContext.GetCustom()),
                    Encoding.UTF8,
                    JsonContentType);
            }
        }
    }
}
