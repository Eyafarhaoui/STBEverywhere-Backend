using System.Text;
using System.Security.Cryptography;

namespace STBEverywhere_back_APICarte.Services
{
    public class CvvGeneratorService : ICvvGeneratorService
    {
        private readonly string _secretKey;

        public CvvGeneratorService(IConfiguration configuration)
        {
            _secretKey = configuration["BankSecrets:CvvSecretKey"];
        }

        public string GenerateSecureCvv(string cardNumber, DateTime expirationDate)
        {
            // Normaliser les données d'entrée
            string normalizedCardNumber = cardNumber.Replace(" ", "").Trim();
            string expirationMonth = expirationDate.Month.ToString("D2");
            string expirationYear = expirationDate.Year.ToString().Substring(2, 2);

            // Créer la donnée à hacher
            string dataToHash = $"{normalizedCardNumber}|{expirationMonth}{expirationYear}|{_secretKey}";

            // Générer le hash
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));

            // Convertir le hash en valeur numérique et extraire 3 chiffres
            int numericHash = BitConverter.ToInt32(hashBytes, 0);
            int cvvNumber = Math.Abs(numericHash % 1000); // 3 chiffres

            return cvvNumber.ToString("D3");
        }

        public bool ValidateCvv(string cardNumber, DateTime expirationDate, string cvv)
        {
            string expectedCvv = GenerateSecureCvv(cardNumber, expirationDate);
            return expectedCvv == cvv;
        }
    }
}
