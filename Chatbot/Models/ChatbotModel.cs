using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Chatbot.Models
{
    public class Intent
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; }

        [JsonPropertyName("patterns")]
        public List<string> Patterns { get; set; }

        [JsonPropertyName("responses")]
        public List<string> Responses { get; set; }
    }

    public class Fallback
    {
        [JsonPropertyName("fr")]
        public string Fr { get; set; }

        [JsonPropertyName("ar")]
        public string Ar { get; set; }
    }

    public class ChatbotData
    {
        [JsonPropertyName("intents")]
        public List<Intent> Intents { get; set; }

        [JsonPropertyName("fallback")]
        public Fallback Fallback { get; set; }
    }
}