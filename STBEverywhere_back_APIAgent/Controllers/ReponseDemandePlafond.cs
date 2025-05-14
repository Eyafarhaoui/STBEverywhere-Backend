using Microsoft.AspNetCore.Mvc;
using STBEverywhere_back_APIAgent.Service.IService;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models.DTO;
using System.Security.Claims;
using STBEverywhere_ApiAuth.Repositories;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using STBEverywhere_Back_SharedModels.Models;
using System.Net.Http.Headers;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace STBEverywhere_back_APIAgent.Controllers
{
    [Route("api/agent")]
    [ApiController]
    public class ReponseDemandePlafondController : ControllerBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ReponseDemandePlafondController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ReponseDemandePlafondController(
            IHttpContextAccessor httpContextAccessor,
            ILogger<ReponseDemandePlafondController> logger,
            IUserRepository userRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _userRepository = userRepository;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpPost("repondre-demande-plafond")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RepondreDemandePlafond([FromBody] ReponseDemandeAugmentationPlafondDto dto) 
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);
                if (agent == null)
                {
                    return BadRequest("Agent introuvable pour cet utilisateur.");
                }

                var client = _httpClientFactory.CreateClient();
                var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token?.Replace("Bearer ", ""));

                // Utilisation de 'dto' au lieu de 'reponse'
                var response = await client.PostAsJsonAsync(
                    "http://localhost:5132/api/carte/repondre-demande-augmentation",
                    dto);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erreur API Carte: {StatusCode} - {Content}",
                        response.StatusCode, errorContent);
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                return Ok(await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur traitement demande plafond");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }

        [HttpGet("demandes-plafond-en-attente")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDemandesPlafondEnAttente()
        {
            try
            {
                // Récupération de l'agent
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);
                if (agent == null || string.IsNullOrEmpty(agent.AgenceId))
                {
                    return BadRequest("Agent introuvable ou agence non assignée.");
                }

                // Configuration du client HTTP
                var client = _httpClientFactory.CreateClient();
                var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token?.Replace("Bearer ", ""));

                // Appel à l'API Carte
                var response = await client.GetAsync(
                    $"http://localhost:5132/api/carte/getDemandesPlafondByAgence/{agent.AgenceId}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erreur lors de l'appel à l'API Carte: {StatusCode} - {Content}",
                        response.StatusCode, errorContent);
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var demandes = await response.Content.ReadFromJsonAsync<IEnumerable<DemandeAugmentationPlafond>>();
                var demandesEnAttente = demandes?.Where(d => d.Statut == "EnAttente").ToList();

                if (demandesEnAttente == null || !demandesEnAttente.Any())
                {
                    return NotFound("Aucune demande en attente trouvée pour votre agence.");
                }

                return Ok(demandesEnAttente);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes de plafond");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }

        private int GetUserIdFromToken()
        {
            try
            {
                var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader))
                {
                    throw new UnauthorizedAccessException("Header Authorization manquant");
                }

                var tokenParts = authHeader.Split(' ');
                if (tokenParts.Length != 2 || !tokenParts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Format d'autorisation invalide");
                }

                var token = tokenParts[1].Trim();
                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                {
                    throw new UnauthorizedAccessException("Le token n'est pas un JWT valide");
                }

                var jwtToken = handler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub ||
                    c.Type == ClaimTypes.NameIdentifier);

                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    throw new UnauthorizedAccessException("Claim d'identifiant utilisateur invalide");
                }

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans GetUserIdFromToken");
                throw new UnauthorizedAccessException("Erreur de traitement du token", ex);
            }
        }

        [HttpGet("demandes-carte/by-agence")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDemandesCarteByAgence()
        {
            try
            {
                // Récupération de l'agent et son agence
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);
                if (agent == null || string.IsNullOrEmpty(agent.AgenceId))
                {
                    return BadRequest("Agent introuvable ou agence non assignée.");
                }

                // Configuration du client HTTP
                var client = _httpClientFactory.CreateClient();
                var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token?.Replace("Bearer ", ""));

                // Appel à l'API Carte pour récupérer les demandes
                var response = await client.GetAsync(
                    $"http://localhost:5132/api/carte/demandes-carte/by-agence/{agent.AgenceId}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erreur API Carte: {StatusCode} - {Content}",
                        response.StatusCode, errorContent);
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var demandes = await response.Content.ReadFromJsonAsync<object>();
                return Ok(demandes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes de carte par agence");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur serveur" });
            }
        }

        [HttpPatch("demandes-carte/{demandeId}/statut")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateStatutDemandeCarte(int demandeId, [FromBody] UpdateStatutDemandeDto updateDto)
        {
            try
            {
                // Récupération de l'agent
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);
                if (agent == null)
                {
                    return BadRequest("Agent introuvable.");
                }

                // Configuration du client HTTP
                var client = _httpClientFactory.CreateClient();
                var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token?.Replace("Bearer ", ""));

                // Appel à l'API Carte pour mettre à jour le statut
                var response = await client.PatchAsJsonAsync(
                    $"http://localhost:5132/api/carte/demandes-carte/{demandeId}/statut",
                    updateDto);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erreur API Carte: {StatusCode} - {Content}",
                        response.StatusCode, errorContent);
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var result = await response.Content.ReadFromJsonAsync<object>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la mise à jour du statut de la demande {demandeId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur serveur" });
            }
        }
    }
}
