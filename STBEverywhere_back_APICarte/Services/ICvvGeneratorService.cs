namespace STBEverywhere_back_APICarte.Services
{
    public interface ICvvGeneratorService
    {
        string GenerateSecureCvv(string cardNumber, DateTime expirationDate);
        bool ValidateCvv(string cardNumber, DateTime expirationDate, string cvv);
    }
}
