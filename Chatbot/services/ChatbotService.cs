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
        private readonly Dictionary<string, List<string>> _localSynonyms;
        private readonly string _togetherApiKey;
        private readonly LocalSynonymService _localSynonymService;

        public ChatbotService(IIntentRepository repository,
                            ILogger<ChatbotService> logger,
                            HttpClient httpClient,
                            IMemoryCache memoryCache,
                            IConfiguration configuration,
                            LocalSynonymService localSynonymService)
        {
            _repository = repository;
            _logger = logger;
            _httpClient = httpClient;
            _synonymCache = memoryCache;
            _togetherApiKey = configuration["TogetherAI:ApiKey"];
            _localSynonymService = localSynonymService;

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

            _localSynonyms = new Dictionary<string, List<string>>
            {
                ["financement court terme"] = new List<string> { "prêt rapide", "avance immédiate", "crédit express" },
                ["découvert"] = new List<string> { "avance", "facilité de caisse" },
                ["prêt logement"] = new List<string> { "crédit immobilier", "emprunt maison" },
                ["carte bancaire"] = new List<string> { "carte de crédit", "carte Visa", "carte Mastercard" },
                ["virement"] = new List<string> { "transfert", "versement" },
                ["compte"] = new List<string> { "dépôt", "livret" }
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

                return await GenerateResponseWithTogetherAI(rawResponse, userMessage, language);
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

            // Supprimer la ponctuation et normaliser les espaces
            var normalized = Regex.Replace(text.ToLowerInvariant(), @"[^\w\s]", " ")
                                .Replace("\n", " ")
                                .Replace("\r", " ")
                                .Trim();

            // Supprimer les espaces multiples
            normalized = Regex.Replace(normalized, @"\s+", " ");

            return normalized;
        }

        private bool IsExactMatch(string userMessage, string pattern)
        {
            // Vérifie si le pattern est contenu dans le message ou vice versa
            return userMessage.Contains(pattern) || pattern.Contains(userMessage);
        }

        private double CalculatePartialMatchScore(string userMessage, string pattern)
        {
            var userWords = userMessage.Split(' ');
            var patternWords = pattern.Split(' ');

            // Score basé sur le nombre de mots du pattern présents dans le message
            var matchedWords = patternWords.Count(pw => userWords.Contains(pw));
            var ratio = (double)matchedWords / patternWords.Length;

            // Donne plus de poids aux patterns plus longs
            return ratio > 0.5 ? ratio * patternWords.Length * 0.5 : 0;
        }

        private async Task<double> CalculateSimilarityScore(string userMessage, string pattern, string language)
        {
            try
            {
                // 1. Vérification des synonymes locaux
                var localSynonyms = GetLocalSynonyms(pattern);
                if (localSynonyms.Any(s => userMessage.Contains(s)))
                {
                    return 1.0;
                }

                // 2. Vérification des synonymes externes
                var externalSynonyms = await GetSynonymsAsync(pattern, language);
                if (externalSynonyms.Any(s => userMessage.Contains(s)))
                {
                    return 1.0;
                }

                // 3. Calcul de similarité textuelle (Jaro-Winkler)
                var similarity = CalculateJaroWinklerSimilarity(userMessage, pattern);
                return similarity > 0.85 ? similarity * 1.5 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private double CalculateJaroWinklerSimilarity(string a, string b)
        {
            // Implémentation simplifiée de la distance de Jaro-Winkler
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

            // Facteur de préfixe (Winkler)
            int prefixLength = 0;
            int maxPrefix = Math.Min(4, Math.Min(a.Length, b.Length));
            while (prefixLength < maxPrefix && a[prefixLength] == b[prefixLength])
                prefixLength++;

            return jaro + prefixLength * 0.1 * (1 - jaro);
        }

        private async Task<string> GenerateResponseWithTogetherAI(string rawResponse, string userMessage, string language)
        {
            if (string.IsNullOrWhiteSpace(_togetherApiKey))
            {
                _logger.LogWarning("Clé API Together.ai non configurée");
                return rawResponse ?? GetFallbackMessage(language);
            }

            try
            {
                var systemPrompt = rawResponse != null
                    ? $"Tu es un assistant bancaire expert de la STB. Reformule cette réponse technique en {language} pour qu'elle soit claire et utile pour le client:\n{rawResponse}\n\nRéponse reformulée :"
                    : $"Tu es un assistant bancaire expert de la STB. Réponds en {language} de manière professionnelle et précise à la question du client sur les services bancaires.";

                var request = new
                {
                    model = "mistralai/Mistral-7B-Instruct-v0.1",
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
                    temperature = 0.3, // Plus bas pour des réponses plus factuelles
                    max_tokens = 500
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _togetherApiKey);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var response = await httpClient.PostAsync(
                    "https://api.together.xyz/v1/chat/completions",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Erreur Together.ai ({(int)response.StatusCode}): {errorContent}");
                    return rawResponse ?? GetFallbackMessage(language);
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var togetherResponse = JsonSerializer.Deserialize<TogetherAIResponse>(jsonResponse, _jsonOptions);

                return togetherResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
                    ?? rawResponse
                    ?? GetFallbackMessage(language);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur Together.ai");
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

        private async Task<List<string>> GetSynonymsAsync(string term, string language)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new List<string>();

            if (_synonymCache.TryGetValue(term, out List<string> cachedSynonyms))
                return cachedSynonyms;

            try
            {
                List<string> synonyms = language == "en"
                    ? await GetEnglishSynonyms(term)
                    : _localSynonymService.GetSynonyms(term);

                var localSynonyms = GetLocalSynonyms(term);
                synonyms = synonyms.Union(localSynonyms).ToList();

                _synonymCache.Set(term, synonyms, TimeSpan.FromHours(1));
                return synonyms;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la récupération des synonymes pour '{term}'");
                return GetLocalSynonyms(term);
            }
        }

        private async Task<List<string>> GetEnglishSynonyms(string term)
        {
            var url = $"https://api.datamuse.com/words?rel_syn={Uri.EscapeDataString(term)}&max=5";
            using var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var json = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<List<DatamuseResponse>>(json, _jsonOptions);

            return results?
                .Where(r => !string.IsNullOrWhiteSpace(r.Word))
                .Select(r => r.Word.ToLowerInvariant().Trim())
                .Distinct()
                .ToList() ?? new List<string>();
        }

        private List<string> GetLocalSynonyms(string term)
        {
            return _localSynonyms.TryGetValue(term, out var synonyms) ? synonyms : new List<string>();
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

        private record DatamuseResponse(string Word, int Score);

        private class TogetherAIResponse
        {
            public List<TogetherAIChoice> Choices { get; set; }

            public class TogetherAIChoice
            {
                public TogetherAIMessage Message { get; set; }
            }

            public class TogetherAIMessage
            {
                public string Content { get; set; }
            }
        }
    }
}