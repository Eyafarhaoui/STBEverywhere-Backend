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
using STBEverywhere_back_APIClient.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using STBEverywhere_Back_SharedModels.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Net;

namespace STBEverywhere_back_APIAgent.Controllers
{
    [Route("api/agent")]
    [ApiController]
    public class AgentController : ControllerBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AgentController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly HttpClient _httpClient;

        public AgentController(
            IHttpContextAccessor httpContextAccessor,
            ILogger<AgentController> logger, HttpClient httpClient,
            IUserRepository userRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _userRepository = userRepository;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _context = context;
            _environment = environment;
            _httpClient = httpClient;
        }








        [HttpGet("statistiques-KYC")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatistiquesKYCAgent()
        {
            try
            {
                _logger.LogInformation("Démarrage de l'endpoint GetStatistiquesKYCAgent");

                // 1) Récupérer l'agent connecté
                var userId = GetUserIdFromToken();
                _logger.LogInformation("UserId extrait du token: {UserId}", userId);

                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null)
                {
                    _logger.LogWarning(" Agent introuvable pour l'utilisateur {UserId}", userId);
                    return BadRequest("Agent introuvable.");
                }

                if (string.IsNullOrEmpty(agent.AgenceId))
                {
                    _logger.LogWarning(" L'agent {AgentId} n'a pas d'agence définie.", agent.Id);
                    return BadRequest("Agence non définie pour cet agent.");
                }

                _logger.LogInformation("Agent trouvé: Id={AgentId}, AgenceId={AgenceId}", agent.Id, agent.AgenceId);

                var apiUrl = $"http://localhost:5260/api/Client/getDemandesKYCByAgence/{agent.AgenceId}";
                _logger.LogInformation("Appel de l'API: {ApiUrl}", apiUrl);

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.GetAsync(apiUrl);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, " Erreur réseau lors de l'appel à l'API.");
                    return StatusCode(500, "Erreur de communication avec le service.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation(" Aucune demande trouvée pour l'agence {AgenceId}.", agent.AgenceId);
                    return Ok(new { Total = 0, EnAttente = 0, Traitees = 0 });
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(" L'API a retourné un statut non succès: {StatusCode} avec message: {ErrorContent}", response.StatusCode, errorContent);
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var demandes = await response.Content.ReadFromJsonAsync<IEnumerable<ModificationRequest>>();

                if (demandes == null || !demandes.Any())
                {
                    _logger.LogInformation(" Aucun résultat retourné par l'API.");
                    return Ok(new { Total = 0, EnAttente = 0, Traitees = 0 });
                }

                // 3) Logs détaillés des demandes récupérées
                _logger.LogInformation(" {NbDemandes} demandes récupérées de l'API.", demandes.Count());

                foreach (var demande in demandes)
                {
                    _logger.LogInformation(
                        "🔎 Demande Id={Id}, ClientId={ClientId}, Status={Status}, ProcessedByAgentId={ProcessedBy}, ProcessedDate={ProcessedDate}",
                        demande.Id, demande.ClientId, demande.Status, demande.ProcessedByAgentId, demande.ProcessedDate);
                }

                var today = DateTime.Today;
                _logger.LogInformation(" Date d'aujourd'hui pour le filtrage : {Today}", today);

                // 4) Comptage
                var nbEnAttente = demandes.Count(d => d.Status == "EnCours");
                _logger.LogInformation("📥 Nombre de demandes en attente (Status='EnCours') : {NbEnAttente}", nbEnAttente);

                var nbTraitees = demandes.Count(d =>
                    d.Status != "EnCours" &&
                    d.ProcessedByAgentId == agent.Id &&
                    d.ProcessedDate.HasValue &&
                    d.ProcessedDate.Value.Date == today
                );
                _logger.LogInformation("Nombre de demandes traitées aujourd'hui par l'agent {AgentId} : {NbTraitees}", agent.Id, nbTraitees);

                var total = nbEnAttente + nbTraitees;
                _logger.LogInformation("Total calculé : {Total}", total);

                // 5) Retourner la réponse
                return Ok(new
                {
                    Total = total,
                    EnAttente = nbEnAttente,
                    Traitees = nbTraitees
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Erreur interne lors de la récupération des statistiques.");
                return StatusCode(500, "Erreur interne du serveur.");
            }
        }
























        







        #region Demandes de modification client
        [HttpGet("modification-requests/pending")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPendingModificationRequests()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null || string.IsNullOrEmpty(agent.AgenceId))
                {
                    return BadRequest("Agent introuvable ou agence non assignée.");
                }

                var requests = await _context.ModificationRequests
                    .Include(r => r.Client)
                    .Where(r => r.Status == "EnCours" && r.Client.AgenceId == agent.AgenceId)
                    .OrderBy(r => r.RequestDate)
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes de modification");
                return StatusCode(500, new { message = "Erreur interne du serveur" });
            }
        }
        [HttpGet("modification-requests/{requestId}/document")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModificationRequestDocument(int requestId)
        {
            try
            {
                // 1. Vérification de l'autorisation de l'agent
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null || string.IsNullOrEmpty(agent.AgenceId))
                {
                    return Unauthorized(new { message = "Agent non autorisé" });
                }

                // 2. Vérification que la demande appartient à un client de la même agence
                var request = await _context.ModificationRequests
                    .Include(r => r.Client)
                    .FirstOrDefaultAsync(r => r.Id == requestId && r.Client.AgenceId == agent.AgenceId);

                if (request == null)
                {
                    return NotFound("Demande non trouvée ou non autorisée");
                }

                // 3. Configuration du client HTTP
                var httpClient = _httpClientFactory.CreateClient();
                var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token?.Replace("Bearer ", ""));

                // 4. Appel à l'API Client pour récupérer le document
                var response = await httpClient.GetAsync(
                    $"http://localhost:5260/api/client/modification-requests/{requestId}/document",
                    HttpCompletionOption.ResponseHeadersRead); // Important: ne pas charger tout le contenu en mémoire

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erreur API Client: {StatusCode} - {Content}",
                        response.StatusCode, errorContent);
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                // 5. Récupération du flux directement
                var stream = await response.Content.ReadAsStreamAsync();

                // 6. Détermination du type de contenu et du nom de fichier
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                var fileName = response.Content.Headers.ContentDisposition?.FileName ??
                             $"Justificatif_{request.Client?.Nom}_{request.FieldToModify}{Path.GetExtension(request.JustificationPath)}";

                // 7. Retourner le flux directement sans le charger en mémoire
                return File(stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la récupération du document {requestId}");
                return StatusCode(500, new { message = "Erreur interne du serveur" });
            }
        }
        [HttpPost("modification-requests/{requestId}/process")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ProcessModificationRequest(int requestId, [FromBody] ProcessRequestDto dto)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var agent = await _userRepository.GetAgentByUserIdAsync(userId);

                if (agent == null || string.IsNullOrEmpty(agent.AgenceId))
                {
                    return Unauthorized(new { message = "Agent non autorisé" });
                }

                var request = await _context.ModificationRequests
                    .Include(r => r.Client)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    return NotFound("Demande non trouvée");
                }

               

                if (dto.Approve)
                {
                    // Appliquer la modification
                    switch (request.FieldToModify.ToLower())
                    {
                        case "adresse":
                            request.Client.Adresse = request.NewValue;
                            break;
                        case "profession":
                            request.Client.Profession = request.NewValue;
                            break;
                        case "situationprofessionnelle":
                            request.Client.SituationProfessionnelle = request.NewValue;
                            break;
                        case "etatcivil":
                            request.Client.EtatCivil = request.NewValue;
                            break;
                        case "residence":
                            request.Client.Residence = request.NewValue;
                            break;
                    }

                    request.Status = "Acceptee";
                }
                else
                {
                    request.Status = "Refusee";
                }

                request.ProcessedByAgentId = agent.Id;
                request.ProcessedDate = DateTime.Now;
               // request.ResponseComment = dto.Comment;

                await _context.SaveChangesAsync();

                // Envoyer une notification au client
                // await _notificationService.NotifyModificationRequestStatus(request.ClientId, request.Id, request.Status);

                return Ok(new
                {
                    message = $"Demande {request.Status}",
                    status = request.Status,
                    processedDate = request.ProcessedDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors du traitement de la demande {requestId}");
                return StatusCode(500, new { message = "Erreur interne du serveur" });
            }
        }
        #endregion

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
     
    }

}