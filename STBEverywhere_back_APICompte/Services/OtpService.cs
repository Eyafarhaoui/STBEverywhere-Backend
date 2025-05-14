using RestSharp;
using STBEverywhere_back_APICompte.Services.IServices;
using STBEverywhere_Back_SharedModels.Data;

namespace STBEverywhere_back_APICompte.Services
{
    public class OtpService : IOtpService
    {
        private readonly ILogger<OtpService> _logger;

        public OtpService(ILogger<OtpService> logger)
        {
           
            _logger = logger;
        }


        public string GenerateOtp()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString(); // 6 chiffres
        }

        public async Task SendOtpAsync(string mobile, string otp)
        {


            var smsRequestEmetteur = new
            {
                mobile = mobile,
                message = $"otp est  {otp} "

            };

            var options = new RestClientOptions("http://localhost:5203")
            {
                MaxTimeout = -1,
            };
            var clientrest = new RestClient(options);

            var requestEmetteur = new RestRequest("/Send", Method.Post);
            requestEmetteur.AddHeader("Content-Type", "application/json");
            requestEmetteur.AddJsonBody(smsRequestEmetteur);
            RestResponse responseEmetteur = await clientrest.ExecuteAsync(requestEmetteur);



            Console.WriteLine($"Envoi SMS à {mobile}: Votre code OTP est {otp}");
            await Task.CompletedTask;
        }

        public bool ValidateOtp(string storedOtp, string providedOtp)
        {
            _logger.LogInformation("Validation OTP - Code reçu: {providedOtp}, Code attendu: {storedOtp}");
   
            return storedOtp == providedOtp;
        }
    }
}
