// LocalSynonymService.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chatbot.Services
{
    public class LocalSynonymService
    {
        private readonly Dictionary<string, float[]> _wordEmbeddings;

        public LocalSynonymService()
        {
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
            if (!_wordEmbeddings.ContainsKey(term))
                return new List<string>();

            var termVector = _wordEmbeddings[term];
            var synonyms = new List<string>();

            foreach (var entry in _wordEmbeddings)
            {
                if (entry.Key != term && CosineSimilarity(termVector, entry.Value) >= similarityThreshold)
                {
                    synonyms.Add(entry.Key);
                }
            }

            return synonyms;
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