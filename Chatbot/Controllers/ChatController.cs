using Chatbot.Models;
using Chatbot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Chatbot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatbotService _chatbotService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ChatbotService chatbotService, ILogger<ChatController> logger)
        {
            _chatbotService = chatbotService;
            _logger = logger;
        }

        public class ChatRequest
        {
            public string Message { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Message))
                {
                    _logger.LogWarning("Received empty chat request");
                    return BadRequest(new { error = "Le message est requis" });
                }

                _logger.LogInformation($"Processing message: {request.Message}");
                var response = await _chatbotService.GetResponseAsync(request.Message);
                return Ok(new { reply = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request");
                return StatusCode(500, new { error = "Une erreur interne s'est produite" });
            }
        }

        [HttpPut("intents/{tag}")]
        public async Task<IActionResult> UpdateIntent([FromRoute] string tag, [FromBody] Intent updatedIntent)
        {
            try
            {
                if (updatedIntent == null || updatedIntent.Tag != tag)
                {
                    return BadRequest("Invalid intent data");
                }

                await _chatbotService.UpdateIntentAsync(updatedIntent);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating intent {tag}");
                return StatusCode(500, new { error = ex.Message });
            }
        }




        public class Chatrequest
        {
            public string Message { get; set; }
        }


        [HttpPost, Route("api/students/chat")]
        public async Task<IActionResult> ChatWithGPT([FromBody] Chatrequest request)
        {
            if (request == null || request.Message == null /*|| request.Message.Count == 0*/)
            {
                return BadRequest("Requête invalide.");
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "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");
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = request.Message
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                return StatusCode((int)response.StatusCode, responseContent);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Erreur serveur : " + ex.Message);
            }
        }








        [HttpPost("reload")]
        public async Task<IActionResult> ReloadData()
        {
            try
            {
                await _chatbotService.ReloadDataAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading chatbot data");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}