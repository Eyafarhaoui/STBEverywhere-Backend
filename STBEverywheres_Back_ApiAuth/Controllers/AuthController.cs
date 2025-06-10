using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywheres_Back_ApiAuth.Services;
using System.Security.Claims;

namespace STBEverywheres_Back_ApiAuth.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserRepository _userRepository; // Ajout du repository User
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, IUserRepository userRepository, ILogger<AuthController> logger)
        {
            _authService = authService;
            _userRepository = userRepository;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                _logger.LogInformation("Tentative de connexion pour l'email : {Email}", loginDto.Email);

                // Authentifier l'utilisateur avec le nouveau service
                var authResult = await _authService.Authenticate(loginDto.Email, loginDto.Password);
                if (authResult == null)
                {
                    return Unauthorized(new { message = "Email ou mot de passe incorrect." });
                }

                // Retourner la réponse au format similaire à l'ancienne version
                return Ok(new
                {
                    Token = authResult.AccessToken,
                    RefreshToken = authResult.RefreshToken,
                    Role = authResult.Role.ToString(),
                    UserId = authResult.UserId,
                    ClientId = authResult.ClientId,
                    AgentId = authResult.AgentId
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Échec de la connexion pour l'email : {Email}. Raison : {Message}", loginDto.Email, ex.Message);

                // Modifier ici pour retourner directement le remainingSeconds au bon niveau
                return Unauthorized(new
                {
                    message = ex.Message,
                    remainingSeconds = ex.InnerException != null && int.TryParse(ex.InnerException.Message, out int sec) ? sec : (int?)null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Une erreur s'est produite lors de la connexion pour l'email : {Email}. Erreur : {Message}", loginDto.Email, ex.Message);
                return StatusCode(500, new { Message = "Une erreur interne s'est produite." });
            }
        }


        [HttpPost("refresh-token")]
       
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["RefreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new { message = "Refresh token is required" });

            try
            {
                var result = await _authService.RefreshToken(refreshToken);
                SetTokenCookies(result.AccessToken, result.RefreshToken);
                return Ok(result);
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning(ex, "Invalid refresh token");
                return Unauthorized(new { message = ex.Message });
            }
        }
        [HttpGet("tokens")]
        public IActionResult GetTokens()
        {
            var accessToken = Request.Cookies["AccessToken"];
            var refreshToken = Request.Cookies["RefreshToken"];

            _logger.LogInformation("Vérification des cookies dans la requête:");
            _logger.LogInformation($"AccessToken: {(string.IsNullOrEmpty(accessToken) ? "Non trouvé" : accessToken)}");
            _logger.LogInformation($"RefreshToken: {(string.IsNullOrEmpty(refreshToken) ? "Non trouvé" : refreshToken)}");

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("Aucun token trouvé dans les cookies.");
                return Unauthorized(new { message = "Aucun token trouvé." });
            }

            _logger.LogInformation("Tokens trouvés, envoi au client.");
            return Ok(new { AccessToken = accessToken, RefreshToken = refreshToken });
        }

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("AccessToken");
            Response.Cookies.Delete("RefreshToken");
            return Ok(new { message = "Logged out successfully" });
        }

        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            };

            Response.Cookies.Append("AccessToken", accessToken, cookieOptions);
            Response.Cookies.Append("RefreshToken", refreshToken, cookieOptions);
        }

        //EFvefve

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetDto)
        {
            try
            {
                _logger.LogInformation("Demande de réinitialisation de mot de passe pour le token : {Token}", resetDto.Token);

                // Appel au service pour réinitialiser le mot de passe avec le token
                var result = await _authService.ResetPasswordAsync(resetDto);

                return Ok(new { Message = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la réinitialisation du mot de passe");
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            try
            {
                _logger.LogInformation("Demande de réinitialisation de mot de passe reçue.");

                var result = await _authService.ForgotPasswordAsync(forgotPasswordDto.Email);

                return Ok(new { Message = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la demande de réinitialisation.");
                return BadRequest(new { Message = ex.Message });
            }
        }

    }
}