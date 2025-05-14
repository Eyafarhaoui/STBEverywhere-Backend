namespace STBEverywhere_back_APICompte.Services.IServices
{
    public interface IOtpService
    {
        string GenerateOtp();
        Task SendOtpAsync(string mobile, string otp);
        bool ValidateOtp(string storedOtp, string providedOtp);
    }
}
