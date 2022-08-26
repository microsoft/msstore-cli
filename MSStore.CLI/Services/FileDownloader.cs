// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal class FileDownloader : IFileDownloader
    {
        public async Task<bool> DownloadAsync(string url, string destinationFileName, IProgress<double> progress, CancellationToken ct = default)
        {
            progress.Report(0);

            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);

                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                    response.EnsureSuccessStatusCode();

                    var contentLength = response.Content.Headers.ContentLength;

                    using var stream = await response.Content.ReadAsStreamAsync(ct);

                    const int bufferSize = 81920;

                    using var file = File.OpenWrite(destinationFileName);
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

                    progress.Report(100);

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
