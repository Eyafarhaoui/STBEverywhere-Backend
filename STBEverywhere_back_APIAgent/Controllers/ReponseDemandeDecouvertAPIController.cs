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
using System.Text.Json;
using System.Text;
using System.Net;


namespace STBEverywhere_back_APIAgent.Controllers
{
    [Route("api/agent")]
    [ApiController]
    public class ReponseDemandeDecouvertAPIController : ControllerBase
    {
        private readonly IDemandeModificationDecouvertService _service;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ReponseDemandeDecouvertAPIController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        public ReponseDemandeDecouvertAPIController(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IDemandeModificationDecouvertService service, ILogger<ReponseDemandeDecouvertAPIController> logger, IUserRepository userRepository,
    IHttpClientFactory httpClientFactory,IConfiguration configuration)

        {
            _httpClient = httpClient;

            _service = service;
            _logger = logger;
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;


        }

        [HttpGet("statistiques-demandes-decouvert")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatistiquesDemandesDecouvert()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null || string.IsNullOrEmpty(agent.AgenceId))
                {
                    return BadRequest("Agent introuvable ou agence non définie.");
                }

                var apiUrl = $"http://localhost:5185/api/Decouvert/getDemandesByAgence/{agent.AgenceId}";
                _logger.LogInformation("Appel de l'API : {ApiUrl}", apiUrl);

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.GetAsync(apiUrl);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Erreur réseau lors de l'appel à l'API découvert.");
                    return StatusCode(500, "Erreur lors de la communication avec le service découvert.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Aucune demande de découvert trouvée pour cette agence.");
                    return Ok(new
                    {
                        Total = 0,
                        EnAttente = 0,
                        Traitees = 0
                    });
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var demandes = await response.Content.ReadFromJsonAsync<IEnumerable<DemandeModificationDecouvert>>();

                if (demandes == null || !demandes.Any())
                {
                    return Ok(new
                    {
                        Total = 0,
                        EnAttente = 0,
                        Traitees = 0
                    });
                }

                var today = DateTime.Today;

                var nbEnAttente = demandes.Count(d => d.StatutDemande == StatutDemandeEnum.EnAttente);
                var nbTraitees = demandes.Count(d =>
                    d.StatutDemande != StatutDemandeEnum.EnAttente &&
                    d.IdAgentRepondant == agent.Id &&
                    d.DateTraitement.HasValue &&
                    d.DateTraitement.Value.Date == today
                );

                var total = nbEnAttente + nbTraitees;

                return Ok(new
                {
                    Total = total,
                    EnAttente = nbEnAttente,
                    Traitees = nbTraitees
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques des demandes de découvert.");
                return StatusCode(500, "Erreur interne du serveur.");
            }
        }



        [HttpPost("reponsedemandedecouvert")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RepondreDemande([FromBody] ReponseDemandeDecouvertDto reponse)
        {
            if (reponse == null)
            {
                return BadRequest(new { message = "Les données de la réponse sont nulles." });
            }

            var userId = GetUserIdFromToken();

            var Agent = await _userRepository.GetAgentByUserIdAsync(userId);
            if (Agent == null)
            {
                return BadRequest(new { message = "Agent introuvable." });
            }

            var agentId = Agent.Id;

            var demande = await _service.GetDemandeByIdAsync(reponse.IdDemande);
            if (demande == null)
            {
                return NotFound(new { message = "Demande introuvable" });
            }

            await _service.RepondreDemandeAsync(demande, reponse.Accepte, reponse.MotifRefus, agentId);

            return Ok(new
            {
                success = true,
                message = "Réponse enregistrée avec succès.",
                data = new
                {
                    idDemande = reponse.IdDemande,
                    status = reponse.Accepte ? "accepted" : "rejected"
                }
            });
        }








        [HttpGet("demandes-en-attente")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDemandesEnAttente()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var Agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (Agent == null)
                {
                    return BadRequest("Agent introuvable pour cet utilisateur.");
                }

                if (string.IsNullOrEmpty(Agent.AgenceId))
                {
                    return BadRequest("L'agent n'a pas d'agence assignée.");
                }

                var apiUrl = $"http://localhost:5185/api/Decouvert/getDemandesByAgence/{Agent.AgenceId}";
                _logger.LogInformation("Appel de l'API : {ApiUrl}", apiUrl);

                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }

                var demandes = await response.Content.ReadFromJsonAsync<IEnumerable<DemandeModificationDecouvert>>();

                // Tri ajouté ici
                var demandesEnAttente = demandes?
                    .Where(d => d.StatutDemande == StatutDemandeEnum.EnAttente)
                    .OrderBy(d => d.DateDemande)
                    .ToList();

                if (demandesEnAttente == null || !demandesEnAttente.Any())
                {
                    return NotFound("Aucune demande en attente trouvée pour votre agence.");
                }

                return Ok(demandesEnAttente);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes en attente");
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