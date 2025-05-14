using Microsoft.AspNetCore.Mvc;
using RestSharp;
using System.Globalization;
using Newtonsoft.Json;
namespace STBEverywhere_Back_ApiUnitaire.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExchangeController : ControllerBase
    {




        [HttpGet("ExchangerRates")]
        public async Task<IActionResult> GetExchangeRates()
        {
            var options = new RestClientOptions("https://openbank.stb.com.tn")
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/api/students/subscription/exchangerate", Method.Get);
            request.AddHeader("Ocp-Apim-Subscription-Key", "55c87b41825244d7b0299f66e3bda7f6");

            RestResponse response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                return StatusCode((int)response.StatusCode, "Erreur lors de l'appel à l'API STB");
            }

            return Content(response.Content!, "application/json");
        }






        [HttpPost("Convertir")]
        public async Task<IActionResult> Convertir([FromBody] ConversionRequest requestDto)
        {
            var options = new RestClientOptions("https://openbank.stb.com.tn")
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var apiRequest = new RestRequest("/api/students/subscription/exchangerate", Method.Get);
            apiRequest.AddHeader("Ocp-Apim-Subscription-Key", "55c87b41825244d7b0299f66e3bda7f6");

            RestResponse response = await client.ExecuteAsync(apiRequest);

            if (!response.IsSuccessful)
            {
                return StatusCode((int)response.StatusCode, "Erreur lors de l'appel à l'API STB");
            }

            var exchangeRates = JsonConvert.DeserializeObject<List<ExchangeRate>>(response.Content);

            if (exchangeRates == null || !exchangeRates.Any())
            {
                return BadRequest("Aucun taux de change trouvé");
            }

            var tauxDevise = exchangeRates.FirstOrDefault(e =>
                e.Devise == $"{requestDto.CibleDevise}/TND" ||
                e.Devise == $"{requestDto.SourceDevise}/TND");

            if (tauxDevise == null)
            {
                return BadRequest("Devise non trouvée");
            }

            decimal priceBuyValue = decimal.Parse(tauxDevise.PriceBuyValue.Replace(',', '.'), CultureInfo.InvariantCulture);
            decimal priceSellValue = decimal.Parse(tauxDevise.PriceSellValue.Replace(',', '.'), CultureInfo.InvariantCulture);

            // ✅ Vérification des taux nuls
            if ((requestDto.Operation == "vente" && priceSellValue == 0) ||
                (requestDto.Operation == "achat" && priceBuyValue == 0))
            {
                return BadRequest("Taux indisponible, conversion impossible.");
            }

            decimal montantConverti = 0;

            if (requestDto.Operation == "vente") // Vente de TND
            {
                if (requestDto.SourceDevise != "TND")
                {
                    return BadRequest("On ne peut vendre que le TND");
                }

                montantConverti = requestDto.Montant / priceSellValue;
            }
            else if (requestDto.Operation == "achat") // Achat de TND
            {
                if (requestDto.CibleDevise != "TND")
                {
                    return BadRequest("On ne peut acheter que le TND");
                }

                montantConverti = requestDto.Montant * priceBuyValue;
            }
            else
            {
                return BadRequest("Opération non reconnue. Utilisez 'achat' ou 'vente'");
            }

            return Ok(new { MontantConverti = Math.Round(montantConverti, 3) });
        }


        public class ConversionRequest
        {
            public decimal Montant { get; set; }
            public string SourceDevise { get; set; }
            public string CibleDevise { get; set; }
            public string Operation { get; set; } // "achat" ou "vente"
        }

        public class ExchangeRate
        {
            public string Date { get; set; }
            public string Devise { get; set; }
            public string PriceBuyValue { get; set; }
            public string PriceSellValue { get; set; }
            public int QUOTITY { get; set; }
        }







    }
}




