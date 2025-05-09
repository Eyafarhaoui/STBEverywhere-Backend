using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;

namespace Chatbot.Services
{
    public class LocalSynonymService
    {
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, List<string>> _localSynonyms;
        private readonly Dictionary<string, float[]> _wordEmbeddings;

        public LocalSynonymService(IMemoryCache cache, HttpClient httpClient)
        {
            _cache = cache;
            _httpClient = httpClient;

            // Synonymes locaux prédéfinis
            _localSynonyms = new Dictionary<string, List<string>>
            {
                ["financement court terme"] = new List<string> { "prêt rapide", "avance immédiate", "crédit express" },
                ["découvert"] = new List<string> { "avance", "facilité de caisse" },
                ["prêt logement"] = new List<string> { "crédit immobilier", "emprunt maison" },
                ["carte bancaire"] = new List<string> { "carte de crédit", "carte Visa", "carte Mastercard" },
                ["virement"] = new List<string> { "transfert", "versement" },
                ["compte"] = new List<string> { "dépôt", "livret" }
            };

            // Embeddings de mots pour similarité sémantique
            _wordEmbeddings = new Dictionary<string, float[]>
            {
                ["financement"] = new float[] { 0.8f, 0.2f, 0.1f },
                ["prêt"] = new float[] { 0.75f, 0.3f, 0.15f },
                ["crédit"] = new float[] { 0.78f, 0.25f, 0.12f },
                ["découvert"] = new float[] { 0.6f, 0.5f, 0.3f },
                ["avance"] = new float[] { 0.58f, 0.52f, 0.28f },
                ["carte"] = new float[] { 0.3f, 0.7f, 0.4f },
                ["bancaire"] = new float[] { 0.35f, 0.65f, 0.38f },
                ["virement"] = new float[] { 0.4f, 0.6f, 0.2f },
                ["transfert"] = new float[] { 0.42f, 0.58f, 0.18f }
            };
        }

        public List<string> GetSynonyms(string term, float similarityThreshold = 0.7f)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new List<string>();

            // Vérifier d'abord le cache
            if (_cache.TryGetValue(term, out List<string> cachedSynonyms))
                return cachedSynonyms;

            // Combiner les différentes sources de synonymes
            var synonyms = new List<string>();

            // 1. Synonymes locaux prédéfinis
            if (_localSynonyms.TryGetValue(term, out var localSynonyms))
            {
                synonyms.AddRange(localSynonyms);
            }

            // 2. Synonymes sémantiques via embeddings
            if (_wordEmbeddings.ContainsKey(term))
            {
                var termVector = _wordEmbeddings[term];
                foreach (var entry in _wordEmbeddings)
                {
                    if (entry.Key != term && CosineSimilarity(termVector, entry.Value) >= similarityThreshold)
                    {
                        synonyms.Add(entry.Key);
                    }
                }
            }

            // 3. Synonymes via ConceptNet (si pas trouvé localement)
            if (synonyms.Count == 0)
            {
                var conceptNetSynonyms = GetConceptNetSynonymsAsync(term).GetAwaiter().GetResult();
                synonyms.AddRange(conceptNetSynonyms);
            }

            // Mettre en cache les résultats
            _cache.Set(term, synonyms.Distinct().ToList(), TimeSpan.FromHours(1));

            return synonyms.Distinct().ToList();
        }

        public List<string> GetLocalSynonyms(string term)
        {
            return _localSynonyms.TryGetValue(term, out var synonyms) ?
                synonyms :
                new List<string>();
        }

        public async Task<List<string>> GetConceptNetSynonymsAsync(string term, string language = "fr")
        {
            if (language != "fr") return new List<string>();

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

                return synonyms.Distinct().ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private float CosineSimilarity(float[] vecA, float[] vecB)
        {
            float dotProduct = 0.0f;
            float magnitudeA = 0.0f;
            float magnitudeB = 0.0f;

            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                magnitudeA += vecA[i] * vecA[i];
                magnitudeB += vecB[i] * vecB[i];
            }

            magnitudeA = (float)Math.Sqrt(magnitudeA);
            magnitudeB = (float)Math.Sqrt(magnitudeB);

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct / (magnitudeA * magnitudeB);
        }
    }
}