// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API;
using MSStore.CLI.Services.Graph;

namespace MSStore.CLI.Services
{
    internal class AzureBlobManager : IAzureBlobManager
    {
        public async Task<string> UploadFileAsync(string blobUri, string localFilePath, IProgress<double> progress, CancellationToken ct)
        {
            using var httpClient = new HttpClient();

            using var request = new HttpRequestMessage(HttpMethod.Put, blobUri.Replace("+", "%2B"));

            using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);

            request.Headers.Add("x-ms-blob-type", "BlockBlob");
            request.Content = new ProgressableStreamContent(new StreamContent(fileStream), progress);

            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(ct);
            }
            else
            {
                throw new MSStoreException(await response.Content.ReadAsStringAsync(ct));
            }
        }
    }
}
