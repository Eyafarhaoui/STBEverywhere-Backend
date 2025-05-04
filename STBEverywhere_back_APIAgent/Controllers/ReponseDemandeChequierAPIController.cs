using Microsoft.AspNetCore.Mvc;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APIAgent.Service.IService;
using STBEverywhere_Back_SharedModels.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace STBEverywhere_back_APIAgent.Controllers
{
    [Route("api/agent")]
    [ApiController]
    public class ReponseDemandeChequierAPIController : ControllerBase
    {
        private readonly IDemandeModificationDecouvertService _service;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ReponseDemandeDecouvertAPIController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        public ReponseDemandeChequierAPIController(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IDemandeModificationDecouvertService service, ILogger<ReponseDemandeDecouvertAPIController> logger, IUserRepository userRepository,
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


        [HttpGet("statistiques-agent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatistiquesAgent()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null || string.IsNullOrEmpty(agent.AgenceId))
                {
                    return BadRequest("Agent non trouvé ou agence non définie.");
                }

            // Appel API pour récupérer les demandes de l'agence
           

                var apiUrl = $"http://localhost:5264/api/DemandeChequierApi/getDemandesChequierByAgence/{agent.AgenceId}";

                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }

                var demandes = await response.Content.ReadFromJsonAsync<IEnumerable<DemandeChequier>>();

                var today = DateTime.Today;

                int demandesEnCours = demandes.Count(d => d.Status == DemandeStatus.EnCoursPreparation);
                int demandesPartiellementTraitees = demandes.Count(d => d.Status == DemandeStatus.DisponibleEnAgence);
                int demandesTraitees = demandes.Count(d =>
                    (d.Status == DemandeStatus.RemisAuClient || d.Status == DemandeStatus.Expedie)
                    && d.IdAgent == agent.Id
                    && d.DateTraitement?.Date == today);

                int totalDemandes = demandesEnCours + demandesPartiellementTraitees + demandesTraitees;

                return Ok(new
                {
                    Total = totalDemandes,
                    EnCours = demandesEnCours,
                    PartiellementTraitees = demandesPartiellementTraitees,
                    Traitees = demandesTraitees
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques de l'agent.");
                return StatusCode(500, "Erreur interne du serveur.");
            }
        }






        [HttpGet("demandes-chequiers-par-rib")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDemandesChequiersParRib(string rib)
        {
            var userId = GetUserIdFromToken();
            var agent = await _userRepository.GetAgentByUserIdAsync(userId);

            if (agent == null)
            {
                return BadRequest("Agent introuvable.");
            }

            var apiCompteUrl = $"http://localhost:5185/api/compte/agence-id?rib={rib}";
            var compteResponse = await _httpClient.GetAsync(apiCompteUrl);
            var compteAgenceId = await compteResponse.Content.ReadAsStringAsync();

            if (!compteResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)compteResponse.StatusCode, compteAgenceId);
            }

            if (compteAgenceId != agent.AgenceId)
            {
                return BadRequest("Cher agent, ce compte n'appartient pas à votre agence.");
            }

            var apiChequierUrl = $"http://localhost:5264/api/DemandeChequierApi/by-rib?rib={rib}";
            var chequierResponse = await _httpClient.GetAsync(apiChequierUrl);
            var demandesBody = await chequierResponse.Content.ReadAsStringAsync();

            if (!chequierResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)chequierResponse.StatusCode, demandesBody);
            }

            return Ok(demandesBody);
        }



        /*

        [HttpGet("demandes-chequiers-par-rib")]
        public async Task<IActionResult> GetDemandesChequiersParRib(string rib)
        {
            var userId = GetUserIdFromToken();
            var agent = await _userRepository.GetAgentByUserIdAsync(userId);

            if (agent == null)
            {
                return BadRequest("Agent introuvable.");
            }

            // 2. Appel HTTP à l'APICompte pour obtenir l'agence du compte
            var apiCompteUrl = $" http://localhost:5185/api/compte/agence-id?rib={rib}";
        
            var compteResponse = await _httpClient.GetAsync(apiCompteUrl);
            var compteAgenceId = await compteResponse.Content.ReadAsStringAsync();

            if (!compteResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)compteResponse.StatusCode, compteAgenceId);
            }

            // 3. Comparaison des agences
            if (compteAgenceId != agent.AgenceId)
            {
                return BadRequest("Cher agent, ce compte n'appartient pas à votre agence.");
            }

            // 4. Appel à l'APIChequier pour obtenir les demandes
            var apiChequierUrl = $"http://localhost:5264/api/DemandeChequierApi/by-rib?rib={rib}";
      
            var chequierResponse = await _httpClient.GetAsync(apiChequierUrl);
            var demandesBody = await chequierResponse.Content.ReadAsStringAsync();

            if (!chequierResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)chequierResponse.StatusCode, demandesBody);
            }

            return Ok(demandesBody);
        }*/




        [HttpPut("changer-statut-demande/{idDemande}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ChangerStatutDemande(int idDemande, [FromQuery] DemandeStatus nouveauStatut)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null)
                {
                    return BadRequest("Agent introuvable.");
                }

                // Construction de l’URL avec les paramètres en query string
                var apiUrl = $"http://localhost:5264/api/DemandeChequierApi/update-statut?NouveauStatut={nouveauStatut}&IdDemande={idDemande}&IdAgent={agent.Id}";

                _logger.LogInformation("Appel de mise à jour du statut : {ApiUrl}", apiUrl);

                var response = await _httpClient.PostAsync(apiUrl, null); // pas de contenu dans le corps

                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, responseBody);
                }

                return Ok(responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du changement de statut de la demande");
                return StatusCode(500, $"Erreur interne : {ex.Message}");
            }
        }









        [HttpGet("demandesChequier-en-attente")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDemandesChequierEnAttente()
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


           

                var apiUrl = $"http://localhost:5264/api/DemandeChequierApi/getDemandesChequierByAgence/{Agent.AgenceId}";
                _logger.LogInformation("Appel de l'API : {ApiUrl}", apiUrl);

                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }

                var demandes = await response.Content.ReadFromJsonAsync<IEnumerable<DemandeChequier>>();

                // Filtrer pour ne garder que les demandes "EnAttente"
                var demandesEnAttente = demandes?.Where(d => d.Status == DemandeStatus.EnCoursPreparation ||  d.Status == DemandeStatus.DisponibleEnAgence).ToList();

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
