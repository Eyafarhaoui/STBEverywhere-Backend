using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class ChatGptController : ControllerBase
{
    private const string API_KEY = "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";

    private const string ENDPOINT = "https://api.openai.com/v1/chat/completions";

    public class pt
    {
        public string prompt { get; set; }
    }
    [HttpPost("chat")]
    public async Task<IActionResult> AskGPT(pt pt)
    {
        var client = new RestClient(new RestClientOptions(ENDPOINT));
        var request = new RestRequest("", Method.Post);

        request.AddHeader("Authorization", $"Bearer {API_KEY}");
        request.AddHeader("Content-Type", "application/json");

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "user", content = pt.prompt }
            }
        };

        request.AddJsonBody(requestBody);

        var response = await client.ExecuteAsync(request);

        if (response.IsSuccessful)
        {
            // Extraire uniquement le texte généré
            var json = JObject.Parse(response.Content);
            var content = json["choices"]?[0]?["message"]?["content"]?.ToString();

            return Content(content ?? "Aucune réponse", "text/plain");
        }
        else
        {
            return StatusCode((int)response.StatusCode, response.Content);
        }
    }
}
