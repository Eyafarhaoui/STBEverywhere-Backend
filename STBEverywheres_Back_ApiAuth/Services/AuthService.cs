using Microsoft.IdentityModel.Tokens;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APIClient.Services;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels.Models.DTO;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace STBEverywheres_Back_ApiAuth.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepository,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResult> Authenticate(string email, string password)
        {
            _logger.LogInformation("Tentative d'authentification pour {Email}", email);

            var user = await _userRepository.GetUserWithClientByEmailAsync(email);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning("Authentication failed for {Email}", email);
                return null;
            }

            return new AuthResult
            {
                AccessToken = GenerateToken(user, isAccessToken: true),
                RefreshToken = GenerateToken(user, isAccessToken: false),
                Role = user.Role,
                UserId = user.Id,
               
            };
        }

        public async Task<AuthResult> RefreshToken(string refreshToken)
        {
            try
            {
                var principal = ValidateToken(refreshToken, isAccessToken: false);
                var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    throw new SecurityTokenException("Invalid token claims");
                }

                var user = await _userRepository.GetByIdAsync(int.Parse(userId));
                if (user == null)
                {
                    throw new SecurityTokenException("User not found");
                }

                return new AuthResult
                {
                    AccessToken = GenerateToken(user, isAccessToken: true),
                    RefreshToken = GenerateToken(user, isAccessToken: false),
                    Role = user.Role,
                    UserId = user.Id,
                   
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token validation failed");
                throw new SecurityTokenException("Invalid refresh token", ex);
            }
        }
        private string GenerateToken(User user, bool isAccessToken)
        {
            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
       // new Claim("clientid", user.Client?.Id.ToString() ?? user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                isAccessToken
                    ? _configuration["Jwt:Key"]
                    : _configuration["Jwt:RefreshKey"]))
            {
                KeyId = isAccessToken ? "access_key" : "refresh_key"
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.Add(
                    isAccessToken
                        ? TimeSpan.FromMinutes(int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"]))
                        : TimeSpan.FromDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"]))),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private ClaimsPrincipal ValidateToken(string token, bool isAccessToken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityKey = isAccessToken
                ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])) { KeyId = "access_key" }
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:RefreshKey"])) { KeyId = "refresh_key" };

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }

        public async Task<string> ForgotPasswordAsync(string email)
        {
            try
            {
                _logger.LogInformation("Password reset request for email: {Email}", email);

                var user = await _userRepository.GetByEmailAsync(email);
                if (user == null)
                {
                    throw new InvalidOperationException("No user found with this email.");
                }
                string salutation = "";
                string destinataire = "";
                if (user.Role == UserRole.Client)
                {
                    var client = await _userRepository.GetClientByUserIdAsync(user.Id);
                    salutation = client?.Genre == "Féminin" ? "Madame" : "Monsieur";
                    destinataire = $"{salutation} {client?.Nom} {client?.Prenom}";
                }
                else if (user.Role == UserRole.Agent)
                {
                    var agent = await _userRepository.GetAgentByUserIdAsync(user.Id);
                    destinataire = $"Bonjour {agent.Nom}";
                }
                var resetToken = Guid.NewGuid().ToString(); // Générer un token unique
                user.ResetPasswordToken = resetToken;
                user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddHours(1); // Expiration après 1h
                await _userRepository.UpdateAsync(user);
               
                // Créer le lien de réinitialisation
                var resetPasswordUrl = $"http://localhost:4200/reset-password?token={resetToken}";
                var emailSubject = "Demande de réinitialisation du mot de passe";
               // var emailBody = $"<p><strong>{destinataire},</strong></p>\r\n<p>Nous avons reçu une demande de réinitialisation de votre mot de passe. Veuillez cliquer sur le lien ci-dessous pour réinitialiser votre mot de passe :</p><p><a href='{resetPasswordUrl}'>Réinitialiser le mot de passe</a></p><p>Ce lien expirera dans 1 heure.</p>";







                var emailBody = $@"
<p><strong>{destinataire},</strong></p>


<p>Un nouveau mot de passe a été demandé pour votre compte Cisco</p>

<p>Si vous n’êtes pas à l’origine de cette opération, nous vous invitons à contacter immédiatement notre service client ou votre conseiller dédié.</p>
<p>Cliquez sur le lien pour terminer la procédure.</p> <p><a href='{resetPasswordUrl}'>Réinitialiser le mot de passe</a></p><p>Ce lien expirera dans 1 heure.</p>
<p>Nous vous rappelons que vos identifiants sont strictement personnels et confidentiels. Ne les communiquez en aucun cas, même à un représentant de la banque.</p>

<p>Merci pour votre confiance.</p>

<p>Cordialement,<br>
<strong>Le Service Client</strong><br>
Société Tunisienne de Banque</p>

<p style='font-size: small; color: gray;'>
<i>Ce message a été généré automatiquement. Merci de ne pas y répondre.</i>
</p>";





                // Appel HTTP POST vers le controller Email
                var emailRequest = new
                {
                    to = user.Email,
                    subject = emailSubject,
                    content = emailBody
                };

                using (var httpClient = new HttpClient())
                {
                    var json = JsonSerializer.Serialize(emailRequest);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    _logger.LogInformation("Appel de l'envoi d'email à {To} avec sujet {Subject}", user.Email, emailSubject);
                    _logger.LogInformation("Body email : {Body}", emailBody);

                    var response = await httpClient.PostAsync("http://localhost:5203/api/email/send", content);

                    if (response.IsSuccessStatusCode)
                    {
                        return "Password reset instructions have been sent to your email.";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Erreur lors de l'envoi de l'email : {Error}", errorContent);
                        throw new Exception("Erreur lors de l'envoi de l'email.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la demande de réinitialisation du mot de passe.");
                throw;
            }
        }


        public async Task<string> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
        {
            _logger.LogInformation("Password reset attempt with token: {Token}", resetPasswordDto.Token);

            var user = await _userRepository.GetByResetTokenAsync(resetPasswordDto.Token);
            if (user == null || user.ResetPasswordTokenExpiry < DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Invalid or expired token.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
            user.ResetPasswordToken = null;
            user.ResetPasswordTokenExpiry = null;

            await _userRepository.UpdateAsync(user);

            return "Password has been reset successfully.";
        }
    }
}