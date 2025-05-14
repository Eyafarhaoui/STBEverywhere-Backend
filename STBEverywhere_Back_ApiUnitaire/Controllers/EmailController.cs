using Microsoft.AspNetCore.Mvc;
using RestSharp;

namespace STBEverywhere_Back_ApiUnitaire.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly ILogger<EmailController> _logger;

        public EmailController(ILogger<EmailController> logger)
        {
            _logger = logger;
        }

        [HttpPost("send")]
         public async Task<IActionResult> SendEmail([FromBody] EmailRequestDto requestDto)
         {
             _logger.LogInformation("Requête reçue pour envoyer un email à {Email}", requestDto.To);

             var emailBody = new
             {
                 from = "stb.digital@stb.com.tn",
                 to = requestDto.To, // Dynamique
                 subject = requestDto.Subject ?? "Demande de chéquier non barré reçue",
                 content = requestDto.Content ?? "<h5>Votre demande a bien été enregistrée et est en cours de traitement.</h5>"
             };

             var options = new RestClientOptions("https://openbank.stb.com.tn")
             {
                 MaxTimeout = -1,
             };

             var client = new RestClient(options);
             var request = new RestRequest("/api/students/subscription/sendmail", Method.Post);
             request.AddHeader("Ocp-Apim-Subscription-Key", "55c87b41825244d7b0299f66e3bda7f6");
             request.AddHeader("Content-Type", "application/json");
             request.AddJsonBody(emailBody);

             var response = await client.ExecuteAsync(request);

             if (response.IsSuccessful)
             {
                 _logger.LogInformation("Email envoyé avec succès à {Email}", requestDto.To);
                 return Ok(new { message = "Email envoyé avec succès." });
             }
             else
             {
                 _logger.LogError("Erreur lors de l'envoi de l'email : {Error}", response.ErrorMessage);
                 return StatusCode((int)response.StatusCode, new { error = "Erreur lors de l'envoi de l'email." });
             }
         }
    }




    public class EmailRequestDto
    {
        public string To { get; set; }  // Adresse email du destinataire
        public string? Subject { get; set; }  // Sujet optionnel
        public string? Content { get; set; }  // Contenu HTML optionnel
       
    }

  
}

