using Microsoft.AspNetCore.Mvc;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APIAgent.Service.IService;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels.Models.DTO;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace STBEverywhere_back_APIAgent.Controllers
{
    [Route("api/agent")]
    [ApiController]
    public class ReponseReclamationAPIController: ControllerBase
    {

        private readonly IReclamationService _service;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ReponseReclamationAPIController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        public ReponseReclamationAPIController(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IReclamationService service, ILogger<ReponseReclamationAPIController> logger, IUserRepository userRepository,
    IHttpClientFactory httpClientFactory, IConfiguration configuration)

        {
            _httpClient = httpClient;

            _service = service;
            _logger = logger;
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;


        }

        [HttpGet("statistiques-reclamations")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatistiquesReclamations()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null || string.IsNullOrEmpty(agent.AgenceId))
                {
                    return BadRequest("Agent introuvable ou agence non définie.");
                }

            // Récupération des réclamations de l’agence
          

                var apiUrl = $"http://localhost:5260/api/Reclamation/reclamations-par-agence/{agent.AgenceId}";
                _logger.LogInformation("Appel de l'API : {ApiUrl}", apiUrl);

                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }

                var reclamations = await response.Content.ReadFromJsonAsync<List<Reclamation>>();
                _logger.LogInformation("Reclamations reçues: {Count}", reclamations?.Count);
                _logger.LogInformation("Première reclamation: {@Reclamation}", reclamations?.FirstOrDefault());
                if (reclamations == null)
                {
                    return StatusCode(500, "Erreur lors de la désérialisation des données.");
                }

                var today = DateTime.Today;

                var enCours = reclamations
                    .Where(r => r.Statut == ReclamationStatut.EnCours)
                    .ToList();

                var traitees = reclamations
                    .Where(r =>
                        r.Statut == ReclamationStatut.traite &&
                        r.IdAgent == agent.Id &&
                        r.DateResolution.HasValue &&
                        r.DateResolution.Value.Date == today
                    ).ToList();

                int nbEnCours = enCours.Count;
                _logger.LogInformation("Reclamations en cours", nbEnCours);
                int nbTraitees = traitees.Count;
                _logger.LogInformation("Reclamations en cours", nbEnCours);
                int total = nbEnCours + nbTraitees;

                return Ok(new
                {
                    Total = total,
                    EnCours = nbEnCours,
                    Traitees = nbTraitees
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques des réclamations.");
                return StatusCode(500, "Erreur interne du serveur.");
            }
        }



        [HttpPost("repondre-reclamation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RepondreAReclamation([FromBody] ReponseReclamationAgentDto dto)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);
                if (agent == null)
                    return BadRequest("Agent introuvable.");

                var success = await _service.RepondreAReclamationAsync(dto.Id, dto.ContenuReponse, agent.Id);
                if (!success)
                    return NotFound("Réclamation non trouvée ou déjà traitée, ou problème lors de l'envoi d'email.");

                
                return Ok(new { message = "Réponse enregistrée et envoyée avec succès." });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la réponse à une réclamation.");
                return StatusCode(500, "Erreur interne du serveur.");
            }
        }


        [HttpGet("reclamations-en-attente")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetReclamationsEnAttente()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null)
                {
                    return BadRequest("Agent introuvable pour cet utilisateur.");
                }

                if (string.IsNullOrEmpty(agent.AgenceId))
                {
                    return BadRequest("L'agent n'a pas d'agence assignée.");
                }

                var apiUrl = $"http://localhost:5260/api/Reclamation/reclamations-par-agence/{agent.AgenceId}";
                _logger.LogInformation("Appel de l'API : {ApiUrl}", apiUrl);

                var response = await _httpClient.GetAsync(apiUrl);

                
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }

                // Désérialiser directement en List<Reclamation>
                var reclamations = await response.Content.ReadFromJsonAsync<List<Reclamation>>();

              
                var reclamationsEnAttente = reclamations?.Where(r => r.Statut == ReclamationStatut.EnCours ).ToList();

                if (reclamationsEnAttente == null || !reclamationsEnAttente.Any())
                {
                    return NotFound("Aucune demande en attente trouvée pour votre agence.");
                }

                return Ok(reclamationsEnAttente);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des reclamations en attente");
                return StatusCode(500, $"Erreur interne du serveur : {ex.Message}");
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
                    _logger.LogError($"Format invalide de l'Authorization Header: {authHeader}");
                    throw new UnauthorizedAccessException("Format d'autorisation invalide");
                }

                var token = tokenParts[1].Trim();
                _logger.LogInformation($"Token extrait : {token}");

                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                {
                    _logger.LogError("Le token fourni n'est pas un JWT valide");

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

















    }
}
