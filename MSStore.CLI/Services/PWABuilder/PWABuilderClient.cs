// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API;

namespace MSStore.CLI.Services.PWABuilder
{
    internal class PWABuilderClient : IPWABuilderClient
    {
        private static readonly string JsonContentType = "application/json";
        private readonly IHttpClientFactory _httpClientFactory;

        public PWABuilderClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        protected virtual void SetRequest(HttpRequestMessage request, object? requestContent)
        {
            if (requestContent != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestContent, requestContent.GetType(), PWASourceGenerationContext.GetCustom()),
                    Encoding.UTF8,
                    JsonContentType);
            }
        }

        private async Task<T> InvokeAsync<T>(
            HttpMethod httpMethod,
            string relativeUrl,
            object? requestContent,
            HttpClient httpClient,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(httpMethod, relativeUrl);

            SetRequest(request, requestContent);

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

            var resource = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(ct), typeof(T), PWASourceGenerationContext.GetCustom());
            if (resource is T result)
            {
                return result;
            }
            else
            {
                throw new MSStoreException(await response.Content.ReadAsStringAsync(ct));
            }
        }

        public async Task GenerateZipAsync(GenerateZipRequest generateZipRequest, string outputZipPath, IProgress<double> progress, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "generatezip");

            SetRequest(request, generateZipRequest);

            progress.Report(0);

            using var httpClient = _httpClientFactory.CreateClient($"{nameof(PWABuilderClient)}/MSIX");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.Content.Headers.ContentDisposition?.DispositionType != "attachment")
            {
                throw new InvalidOperationException("Error while generating Zip file.", new MSStoreException(await response.Content.ReadAsStringAsync(ct)));
            }

            var contentLength = response.Content.Headers.ContentLength;

            using var stream = await response.Content.ReadAsStreamAsync(ct);

            const int bufferSize = 81920;

            using (var file = File.OpenWrite(outputZipPath))
            {
                file.SetLength(0);
                await file.FlushAsync(ct);
                file.Position = 0;
                var buffer = new byte[bufferSize];
                long totalBytesRead = 0;
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) != 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                    totalBytesRead += bytesRead;
                    if (contentLength.HasValue)
                    {
                        progress.Report((float)totalBytesRead * 100 / contentLength.Value);
                    }
                }
            }

            progress.Report(100);
        }

        public async Task<WebManifestFindResponse> FindWebManifestAsync(Uri site, CancellationToken ct)
        {
            using var httpClient = _httpClientFactory.CreateClient($"{nameof(PWABuilderClient)}/API");

            return await InvokeAsync<WebManifestFindResponse>(
                HttpMethod.Get,
                $"FindWebManifest?site={site}",
                null,
                httpClient,
                ct);
        }
    }
}
