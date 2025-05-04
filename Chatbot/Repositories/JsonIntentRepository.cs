using Chatbot.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chatbot.Repositories
{
    public class JsonIntentRepository : IIntentRepository
    {
        private readonly string _filePath;
        private readonly ILogger<JsonIntentRepository> _logger;
        private ChatbotData _cachedData;

        public JsonIntentRepository(IWebHostEnvironment env, ILogger<JsonIntentRepository> logger)
        {
            _logger = logger;
            _filePath = Path.Combine(env.ContentRootPath, "Data", "intents.json");
            _logger.LogInformation($"Intent repository initialized with path: {_filePath}");
        }

        public async Task<ChatbotData> GetIntentsAsync()
        {
            if (_cachedData != null)
                return _cachedData;

            await ReloadDataAsync();
            return _cachedData; // Retourne les données après rechargement
        }

        public async Task UpdateIntentAsync(Intent updatedIntent)
        {
            var data = await GetIntentsAsync();
            var intent = data.Intents.FirstOrDefault(i => i.Tag == updatedIntent.Tag);

            if (intent != null)
            {
                intent.Patterns = updatedIntent.Patterns;
                intent.Responses = updatedIntent.Responses;

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(_filePath, json);
                _logger.LogInformation($"Updated intent: {updatedIntent.Tag}");

                await ReloadDataAsync();
            }
        }

        public async Task ReloadDataAsync()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    throw new FileNotFoundException($"JSON file not found at: {_filePath}");
                }

                var json = await File.ReadAllTextAsync(_filePath);
                _cachedData = JsonSerializer.Deserialize<ChatbotData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidOperationException("Deserialization returned null");

                // Validate data
                if (_cachedData.Intents == null)
                    throw new InvalidDataException("Intents array is missing in JSON");
                if (_cachedData.Fallback == null)
                    throw new InvalidDataException("Fallback object is missing in JSON");

                _logger.LogInformation("Chatbot data reloaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload chatbot data");
                throw;
            }
        }
    }
}