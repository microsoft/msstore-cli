// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Services.CredentialManager;
using MSStore.CLI.Services.Translation.Models;

namespace MSStore.CLI.Services.Translation
{
    /// <summary>
    /// Translates text with the Azure AI Translator Text API.
    /// </summary>
    /// <remarks>
    /// This targets API version 3.0. Version 2026-06-06 is the newer GA release but is not
    /// backward compatible: it nests the request under <c>inputs</c>, returns results under
    /// <c>value</c>, moves target languages into the body, and its headline features expect
    /// a Microsoft Foundry resource. Version 3.0 works with a plain Translator resource key,
    /// which is what users supply here. The version and the wire DTOs are deliberately
    /// isolated so moving to a newer version stays contained to this class and its models.
    /// </remarks>
    internal class AzureAITranslatorService(
        IHttpClientFactory httpClientFactory,
        ICredentialManager credentialManager,
        IConfigurationManager<Configurations> configurationManager,
        IEnvironmentInformationService environmentInformationService,
        ILogger<AzureAITranslatorService> logger) : ITranslationService
    {
        /// <summary>
        /// The name the Translator key is stored under in the OS secure store.
        /// </summary>
        internal const string CredentialKeyName = "AzureAITranslator";

        internal const string KeyEnvironmentVariable = "MSSTORE_TRANSLATOR_KEY";
        internal const string RegionEnvironmentVariable = "MSSTORE_TRANSLATOR_REGION";

        internal const string ApiVersion = "3.0";

        /// <summary>
        /// The documented maximum number of elements in one translate request.
        /// </summary>
        internal const int MaxElementsPerRequest = 1000;

        /// <summary>
        /// The documented maximum number of characters in one translate request, counted
        /// across all target languages. Only one target is ever requested here.
        /// </summary>
        internal const int MaxCharactersPerRequest = 50000;

        private const int MaxRetryAttempts = 3;

        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly ICredentialManager _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
        private readonly IConfigurationManager<Configurations> _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
        private readonly IEnvironmentInformationService _environmentInformationService = environmentInformationService ?? throw new ArgumentNullException(nameof(environmentInformationService));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private Dictionary<string, TranslatorLanguage>? _cachedLanguages;

        public async Task<string> ResolveLanguageAsync(string language, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new TranslationException("No target language was provided for translation.");
            }

            language = language.Trim();

            var languages = await GetSupportedLanguagesAsync(ct);
            if (languages == null)
            {
                // The language list is only used to canonicalize and validate. If it is
                // unavailable, let the service itself reject an invalid code.
                return language;
            }

            var match = languages.Keys.FirstOrDefault(k => string.Equals(k, language, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }

            throw new TranslationException($"'{language}' is not a language supported by Azure AI Translator. See https://learn.microsoft.com/azure/ai-services/translator/language-support for the list of supported codes.");
        }

        public async Task<IReadOnlyList<TranslationResult?>> TranslateAsync(IReadOnlyList<string?> texts, string targetLanguage, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(texts);

            var results = new TranslationResult?[texts.Count];

            // Only non-empty entries are billable and worth sending. Track the original
            // index of each so results line up with the caller's list.
            var pending = new List<int>();
            for (var i = 0; i < texts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(texts[i]))
                {
                    pending.Add(i);
                }
            }

            if (pending.Count == 0)
            {
                return results;
            }

            var key = GetKey();
            if (string.IsNullOrEmpty(key))
            {
                throw new TranslationException(
                    $"No Azure AI Translator key is configured. Set the {KeyEnvironmentVariable} environment variable, or store one with 'msstore settings set-translator-key'.");
            }

            var region = await GetRegionAsync(ct);

            foreach (var batch in CreateBatches(texts, pending))
            {
                var translated = await TranslateBatchAsync([.. batch.Select(i => texts[i]!)], targetLanguage, key, region, ct);

                for (var i = 0; i < batch.Count; i++)
                {
                    results[batch[i]] = i < translated.Count ? translated[i] : null;
                }
            }

            return results;
        }

        /// <summary>
        /// Splits the indexes to translate into batches that respect both documented limits.
        /// A single item longer than the per-request character limit is sent on its own so
        /// the service can report the specific error rather than the batch failing opaquely.
        /// </summary>
        /// <param name="texts">The full list of texts being translated.</param>
        /// <param name="indexes">The indexes of the entries that need translating.</param>
        /// <returns>The indexes grouped into batches.</returns>
        private static List<List<int>> CreateBatches(IReadOnlyList<string?> texts, List<int> indexes)
        {
            var batches = new List<List<int>>();
            var current = new List<int>();
            var currentLength = 0;

            foreach (var index in indexes)
            {
                var length = texts[index]!.Length;

                if (current.Count > 0 &&
                    (current.Count >= MaxElementsPerRequest || currentLength + length > MaxCharactersPerRequest))
                {
                    batches.Add(current);
                    current = [];
                    currentLength = 0;
                }

                current.Add(index);
                currentLength += length;
            }

            if (current.Count > 0)
            {
                batches.Add(current);
            }

            return batches;
        }

        private static bool IsTransient(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.TooManyRequests ||
            statusCode == HttpStatusCode.RequestTimeout ||
            statusCode >= HttpStatusCode.InternalServerError;

        /// <summary>
        /// Translator does not document a Retry-After header on 429, so exponential backoff
        /// with jitter is the primary strategy and the header is only honoured if present.
        /// </summary>
        /// <param name="response">The throttled or failed response.</param>
        /// <param name="attempt">The 1-based attempt number that just failed.</param>
        /// <returns>How long to wait before retrying.</returns>
        private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            {
                return delta;
            }

            if (retryAfter?.Date is DateTimeOffset date)
            {
                var until = date - DateTimeOffset.UtcNow;
                if (until > TimeSpan.Zero)
                {
                    return until;
                }
            }

            var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            return backoff + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        }

        private string? GetKey()
        {
            var fromEnvironment = _environmentInformationService.GetEnvironmentVariable(KeyEnvironmentVariable);
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                return fromEnvironment;
            }

            var stored = _credentialManager.ReadCredential(CredentialKeyName);
            return string.IsNullOrEmpty(stored) ? null : stored;
        }

        private async Task<string?> GetRegionAsync(CancellationToken ct)
        {
            var fromEnvironment = _environmentInformationService.GetEnvironmentVariable(RegionEnvironmentVariable);
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                return fromEnvironment;
            }

            var config = await _configurationManager.LoadAsync(ct: ct);
            return string.IsNullOrEmpty(config.TranslatorRegion) ? null : config.TranslatorRegion;
        }

        private async Task<Dictionary<string, TranslatorLanguage>?> GetSupportedLanguagesAsync(CancellationToken ct)
        {
            if (_cachedLanguages != null)
            {
                return _cachedLanguages;
            }

            try
            {
                using var httpClient = _httpClientFactory.CreateClient(nameof(AzureAITranslatorService));

                // Listing languages requires no authentication.
                using var response = await httpClient.GetAsync($"/languages?api-version={ApiVersion}&scope=translation", ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Could not retrieve the list of supported languages: {StatusCode}.", response.StatusCode);
                    return null;
                }

                var languages = JsonSerializer.Deserialize(
                    await response.Content.ReadAsStringAsync(ct),
                    TranslationSourceGenerationContext.GetCustom().TranslatorLanguagesResponse);

                _cachedLanguages = languages?.Translation;
                return _cachedLanguages;
            }
            catch (Exception err) when (err is not OperationCanceledException)
            {
                _logger.LogWarning(err, "Could not retrieve the list of supported languages.");
                return null;
            }
        }

        private async Task<IReadOnlyList<TranslationResult?>> TranslateBatchAsync(IReadOnlyList<string> texts, string targetLanguage, string key, string? region, CancellationToken ct)
        {
            var body = JsonSerializer.Serialize(
                texts.Select(t => new TranslateInput { Text = t }).ToList(),
                TranslationSourceGenerationContext.GetCustom().ListTranslateInput);

            // The 'from' parameter is deliberately omitted so the service auto-detects the
            // source language inline. A separate /detect call would be metered separately.
            var route = $"/translate?api-version={ApiVersion}&to={Uri.EscapeDataString(targetLanguage)}";

            using var httpClient = _httpClientFactory.CreateClient(nameof(AzureAITranslatorService));

            for (var attempt = 1; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, route)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("Ocp-Apim-Subscription-Key", key);

                // Required for regional and multi-service resources, optional for a global
                // resource. Sending it when present is harmless; omitting it when needed
                // produces a 401.
                if (!string.IsNullOrEmpty(region))
                {
                    request.Headers.Add("Ocp-Apim-Subscription-Region", region);
                }

                using var response = await httpClient.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    if (response.Headers.TryGetValues("X-metered-usage", out var usage))
                    {
                        _logger.LogInformation("Translator billed characters for this request: {MeteredUsage}.", string.Join(',', usage));
                    }

                    var items = JsonSerializer.Deserialize(
                        await response.Content.ReadAsStringAsync(ct),
                        TranslationSourceGenerationContext.GetCustom().ListTranslateResultItem);

                    if (items == null)
                    {
                        throw new TranslationException("The translation service returned an unreadable response.");
                    }

                    return [.. items.Select(item =>
                    {
                        var text = item.Translations?.FirstOrDefault()?.Text;
                        return text == null ? null : new TranslationResult(text, item.DetectedLanguage?.Language);
                    })];
                }

                if (IsTransient(response.StatusCode) && attempt < MaxRetryAttempts)
                {
                    await Task.Delay(GetRetryDelay(response, attempt), ct);
                    continue;
                }

                throw await CreateExceptionAsync(response, ct);
            }
        }

        private async Task<TranslationException> CreateExceptionAsync(HttpResponseMessage response, CancellationToken ct)
        {
            var content = await response.Content.ReadAsStringAsync(ct);

            TranslatorError? error = null;
            try
            {
                error = JsonSerializer.Deserialize(content, TranslationSourceGenerationContext.GetCustom().TranslatorErrorResponse)?.Error;
            }
            catch (JsonException)
            {
                // Fall through to the generic message below.
            }

            if (response.Headers.TryGetValues("X-RequestId", out var requestIds))
            {
                _logger.LogError("Translator request failed. X-RequestId: {RequestId}. Body: {Body}", string.Join(',', requestIds), content);
            }
            else
            {
                _logger.LogError("Translator request failed with {StatusCode}. Body: {Body}", response.StatusCode, content);
            }

            var message = error?.Code switch
            {
                401015 => "The key provided is for the Speech API, but the Text Translation API is required. Check that you copied the key from a Translator resource.",
                >= 401000 and < 402000 => $"Azure AI Translator rejected the credentials. Check the {KeyEnvironmentVariable} value, and set {RegionEnvironmentVariable} if your Translator resource is regional or multi-service rather than global.",
                403001 => "The Azure AI Translator subscription has exceeded its free quota.",
                >= 403000 and < 404000 => "Azure AI Translator refused the operation. This usually means the resource region is wrong or missing.",
                >= 429000 and < 430000 => "Azure AI Translator is throttling this request. The free tier allows 2 million characters per hour, consumed evenly, so a large burst of reviews can be rejected. Try a smaller --top value.",
                400019 or 400036 => "Azure AI Translator does not support the requested target language.",
                400050 => "A review is longer than the maximum length Azure AI Translator accepts.",
                _ => null
            };

            if (message != null)
            {
                return new TranslationException(message);
            }

            return new TranslationException(
                error?.Message is { Length: > 0 } serviceMessage
                    ? $"Azure AI Translator returned an error: {serviceMessage}"
                    : $"Azure AI Translator returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
    }
}
