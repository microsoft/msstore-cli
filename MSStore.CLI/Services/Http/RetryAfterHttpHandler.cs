// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.Http
{
    public sealed class RetryAfterHttpHandler : HttpClientHandler
    {
        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            do
            {
                var httpResponse = await base.SendAsync(request, cancellationToken);
                if (httpResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (httpResponse.Headers.TryGetValues("Retry-After", out var values))
                    {
                        var retryAfter = values.FirstOrDefault();
                        if (int.TryParse(retryAfter, out int delaySeconds))
                        {
                            await Task.Delay(delaySeconds * 1000, cancellationToken);
                            continue;
                        }
                    }
                    await Task.Delay(500, cancellationToken);
                    continue;
                }
                else
                {
                    return httpResponse;
                }
            }
            while (true);
        }
    }
}
