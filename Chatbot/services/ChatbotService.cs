using Chatbot.Models;
using Chatbot.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Chatbot.Services
{
    public class ChatbotService : IDisposable
    {
        private readonly IIntentRepository _repository;
        private readonly ILogger<ChatbotService> _logger;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly Dictionary<string, string> _languageDetectionKeywords;
        private readonly IMemoryCache _synonymCache;
        private readonly string _openAiApiKey;
        private const string OpenAiModel = "gpt-3.5-turbo"; // ou "gpt-4" selon votre abonnement

        public ChatbotService(IIntentRepository repository,
                            ILogger<ChatbotService> logger,
                            HttpClient httpClient,
                            IMemoryCache memoryCache,
                            IConfiguration configuration)
        {
            _repository = repository;
            _logger = logger;
            _httpClient = httpClient;
            _synonymCache = memoryCache;
            _openAiApiKey = configuration["ApiKey"]; // Configuration simplifiée

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            _languageDetectionKeywords = new Dictionary<string, string>
            {
                ["fr"] = "le la les un une des je tu il nous vous ils",
                ["en"] = "the a an i you he she we they"
            };
        }

        public async Task<string> GetResponseAsync(string userMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userMessage))
                    return (await _repository.GetIntentsAsync()).Fallback?.Fr ?? "Veuillez fournir un message.";

                var language = DetectLanguage(userMessage);
                _logger.LogInformation($"Langue détectée : {language}");

                var data = await _repository.GetIntentsAsync();
                var normalizedMessage = NormalizeText(userMessage);

                var intentTasks = data.Intents
                    .Where(intent => intent?.Patterns != null)
                    .Select(async intent =>
                    {
                        double score = 0;
                        foreach (var pattern in intent.Patterns.Where(p => !string.IsNullOrWhiteSpace(p)))
                        {
                            var patternLower = NormalizeText(pattern);
                            var patternWords = patternLower.Split(' ');

                            // Score pour correspondance exacte
                            if (IsExactMatch(normalizedMessage, patternLower))
                            {
                                score += 2 * (1 + patternWords.Length / 5.0);
                                _logger.LogDebug($"Exact match: {patternLower} (+{2 * (1 + patternWords.Length / 5.0)})");
                                continue;
                            }

                            // Score pour correspondance partielle
                            var partialScore = CalculatePartialMatchScore(normalizedMessage, patternLower);
                            score += partialScore;
                            if (partialScore > 0)
                            {
                                _logger.LogDebug($"Partial match: {patternLower} (+{partialScore})");
                                continue;
                            }

                            // Score pour similarité sémantique
                            var similarityScore = await CalculateSimilarityScore(normalizedMessage, patternLower, language);
                            score += similarityScore;
                            if (similarityScore > 0)
                            {
                                _logger.LogDebug($"Similarity match: {patternLower} (+{similarityScore})");
                            }
                        }
                        return new { Intent = intent, Score = score };
                    });

                var scoredMatches = await Task.WhenAll(intentTasks);
                var bestMatch = scoredMatches.OrderByDescending(x => x?.Score ?? 0).FirstOrDefault();

                _logger.LogDebug($"Meilleur match: {bestMatch?.Intent?.Tag} avec score {bestMatch?.Score}");

                string rawResponse = null;
                if (bestMatch?.Score > 1.5) // Seuil ajustable
                {
                    rawResponse = FormatResponse(bestMatch.Intent.Responses);
                }

                return await GenerateResponseWithOpenAI(rawResponse, userMessage, language);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans GetResponseAsync");
                return "Une erreur s'est produite lors du traitement de votre demande.";
            }
        }

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = Regex.Replace(text.ToLowerInvariant(), @"[^\w\s]", " ")
                                .Replace("\n", " ")
                                .Replace("\r", " ")
                                .Trim();

            normalized = Regex.Replace(normalized, @"\s+", " ");
            return normalized;
        }

        private bool IsExactMatch(string userMessage, string pattern)
        {
            return userMessage.Contains(pattern) || pattern.Contains(userMessage);
        }

        private double CalculatePartialMatchScore(string userMessage, string pattern)
        {
            var userWords = userMessage.Split(' ');
            var patternWords = pattern.Split(' ');

            var matchedWords = patternWords.Count(pw => userWords.Contains(pw));
            var ratio = (double)matchedWords / patternWords.Length;

            return ratio > 0.5 ? ratio * patternWords.Length * 0.5 : 0;
        }

        private async Task<double> CalculateSimilarityScore(string userMessage, string pattern, string language)
        {
            try
            {
                // Utilisation directe de ConceptNet pour les synonymes
                var synonyms = await GetConceptNetSynonymsAsync(pattern, language);
                if (synonyms.Any(s => userMessage.Contains(s)))
                {
                    return 1.0;
                }

                var similarity = CalculateJaroWinklerSimilarity(userMessage, pattern);
                return similarity > 0.85 ? similarity * 1.5 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<List<string>> GetConceptNetSynonymsAsync(string term, string language)
        {
            if (string.IsNullOrWhiteSpace(term) || language != "fr")
                return new List<string>();

            if (_synonymCache.TryGetValue(term, out List<string> cachedSynonyms))
                return cachedSynonyms;

            try
            {
                var url = $"https://api.conceptnet.io/query?rel=/r/Synonym&node=/c/{language}/{term}&limit=10";
                var response = await _httpClient.GetStringAsync(url);
                var data = JObject.Parse(response);
                var synonyms = new List<string>();

                foreach (var edge in data["edges"])
                {
                    var word = edge["end"]["term"]?.ToString()?.Split('/')?.Last();
                    if (!string.IsNullOrWhiteSpace(word) && !word.Equals(term, StringComparison.OrdinalIgnoreCase))
                        synonyms.Add(word);
                }

                var distinctSynonyms = synonyms.Distinct().ToList();
                _synonymCache.Set(term, distinctSynonyms, TimeSpan.FromHours(1));
                return distinctSynonyms;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur ConceptNet pour '{term}'");
                return new List<string>();
            }
        }

        private double CalculateJaroWinklerSimilarity(string a, string b)
        {
            if (a == b) return 1.0;

            int maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0) return 0.0;

            int matchDistance = Math.Max(maxLen / 2 - 1, 0);
            bool[] aMatches = new bool[a.Length];
            bool[] bMatches = new bool[b.Length];
            int matches = 0;
            int transpositions = 0;

            for (int i = 0; i < a.Length; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, b.Length);

                for (int j = start; j < end; j++)
                {
                    if (bMatches[j] || a[i] != b[j]) continue;
                    aMatches[i] = true;
                    bMatches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0.0;

            int k = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!aMatches[i]) continue;
                while (!bMatches[k]) k++;
                if (a[i] != b[k]) transpositions++;
                k++;
            }

            double jaro = ((double)matches / a.Length +
                         (double)matches / b.Length +
                         (double)(matches - transpositions / 2) / matches) / 3;

            int prefixLength = 0;
            int maxPrefix = Math.Min(4, Math.Min(a.Length, b.Length));
            while (prefixLength < maxPrefix && a[prefixLength] == b[prefixLength])
                prefixLength++;

            return jaro + prefixLength * 0.1 * (1 - jaro);
        }

        private async Task<string> GenerateResponseWithOpenAI(string rawResponse, string userMessage, string language)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
            {
                _logger.LogWarning("Clé API OpenAI non configurée");
                return rawResponse ?? GetFallbackMessage(language);
            }

            try
            {
                var systemPrompt = rawResponse != null
                    ? $"Tu es un assistant bancaire expert de la STB. Reformule cette réponse technique en {language} pour qu'elle soit claire et utile pour le client:\n{rawResponse}"
                    : $"Tu es un assistant bancaire expert de la STB. Réponds en {language} de manière professionnelle et précise à la question du client sur les services bancaires.";

                var request = new
                {
                    model = OpenAiModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = systemPrompt
                        },
                        new
                        {
                            role = "user",
                            content = userMessage
                        }
                    },
                    temperature = 0.3,
                    max_tokens = 500
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _openAiApiKey);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var response = await httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Erreur OpenAI ({(int)response.StatusCode}): {errorContent}");
                    return rawResponse ?? GetFallbackMessage(language);
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(jsonResponse, _jsonOptions);

                return openAiResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
                    ?? rawResponse
                    ?? GetFallbackMessage(language);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur OpenAI");
                return rawResponse ?? GetFallbackMessage(language);
            }
        }

        private string DetectLanguage(string text)
        {
            var words = text.ToLower().Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var frenchCount = words.Count(w => _languageDetectionKeywords["fr"].Split(' ').Contains(w));
            var englishCount = words.Count(w => _languageDetectionKeywords["en"].Split(' ').Contains(w));

            return frenchCount >= englishCount ? "fr" : "en";
        }

        private string FormatResponse(List<string> responses)
        {
            return string.Join("\n", responses?
                .Where(r => !string.IsNullOrWhiteSpace(r) && r != "+")
                .Select(r => r.Trim()) ?? Array.Empty<string>());
        }

        private string GetFallbackMessage(string language)
        {
            var data = _repository.GetIntentsAsync().Result;
            return language == "en"
                ? "Sorry, I didn't understand your request. Please contact customer service."
                : data.Fallback?.Fr ?? "Désolé, je n'ai pas compris votre demande.";
        }

        public async Task UpdateIntentAsync(Intent updatedIntent)
        {
            if (updatedIntent == null) return;
            await _repository.UpdateIntentAsync(updatedIntent);
        }

        public async Task ReloadDataAsync()
        {
            await _repository.ReloadDataAsync();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private class OpenAIResponse
        {
            public List<OpenAIChoice> Choices { get; set; }

            public class OpenAIChoice
            {
                public OpenAIMessage Message { get; set; }
            }

            public class OpenAIMessage
            {
                public string Content { get; set; }
            }
        }
    }
}