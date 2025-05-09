using Microsoft.AspNetCore.Mvc;
using RestSharp;

namespace STBEverywhere_Back_ApiUnitaire.Controllers
{
    public class SmsController : ControllerBase
    {
        private readonly ILogger<SmsController> _logger;

        public SmsController(ILogger<SmsController> logger)
        {
            _logger = logger;
        }

        [HttpPost("Send")]
        public async Task<IActionResult> SendSms([FromBody] SmsRequest smsRequest)
        {
            try
            {
                var smsBody = new
                {
                    message = smsRequest.Message,
                    mobile = smsRequest.Mobile
                };

                var options = new RestClientOptions("https://openbank.stb.com.tn")
                {
                    MaxTimeout = -1,
                };
                var clientrest = new RestClient(options);
                var request = new RestRequest("/api/students/subscription/sendsms", Method.Post);
                request.AddHeader("Ocp-Apim-Subscription-Key", "55c87b41825244d7b0299f66e3bda7f6");
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(smsBody);

                RestResponse response = await clientrest.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    return Ok(new { message = "SMS envoyé avec succès." });
                }
                else
                {
                    return StatusCode((int)response.StatusCode, new { error = "Erreur lors de l'envoi du SMS." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi du SMS.");
                return StatusCode(500, new { error = "Erreur serveur." });
            }
        }
    }
    // pour mapper JSON envoye par client  vers classe c sharp et eviter decrire mobile,message dans methode 

    public class SmsRequest
    {
        public string Mobile { get; set; }
        public string Message { get; set; }
    }
}

