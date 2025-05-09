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
        private readonly STBEverywhere_back_APIAgent.Service.EmailService _emailService;

        public AgentController(
     IHttpContextAccessor httpContextAccessor,
     ILogger<AgentController> logger,
     IUserRepository userRepository,
     IHttpClientFactory httpClientFactory,
     IConfiguration configuration,
     ApplicationDbContext context,
     IWebHostEnvironment environment,
    STBEverywhere_back_APIAgent.Service.EmailService emailService)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _userRepository = userRepository;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _context = context;
            _environment = environment;
            _emailService = emailService;
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
                var request = await _context.ModificationRequests
                    .Include(r => r.Client)
                    .ThenInclude(c => c.User)  // Important pour récupérer l'email
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    return NotFound("Demande non trouvée");
                }

                var agent = await _userRepository.GetAgentByUserIdAsync(userId);
                if (agent == null || request.Client.AgenceId != agent.AgenceId)
                {
                    return BadRequest("Vous n'êtes pas autorisé à traiter cette demande");
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

                request.ProcessedByAgentId = userId;
                request.ProcessedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Envoyer l'email de notification
                await SendModificationStatusEmail(request);

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

        private async Task SendModificationStatusEmail(ModificationRequest request)
        {
            try
            {
                var clientEmail = request.Client.User?.Email;
                if (string.IsNullOrEmpty(clientEmail))
                {
                    _logger.LogWarning($"Impossible d'envoyer l'email: adresse email manquante pour le client {request.ClientId}");
                    return;
                }

                string subject;
                string message;

                if (request.Status == "Acceptee")
                {
                    subject = "Votre demande de modification a été acceptée";
                    message = $@"
                <h3>Bonjour {request.Client.Prenom} {request.Client.Nom},</h3>
                <p>Nous vous informons que votre demande de modification de <strong>{request.FieldToModify}</strong> a été <strong>acceptée</strong>.</p>
                <p>Votre {request.FieldToModify} a été mis à jour avec la valeur: <strong>{request.NewValue}</strong></p>
                <p>Date de traitement: {request.ProcessedDate?.ToString("dd/MM/yyyy HH:mm")}</p>
                <p>Cordialement,<br>L'équipe de votre banque</p>";
                }
                else
                {
                    subject = "Votre demande de modification a été refusée";
                    message = $@"
                <h3>Bonjour {request.Client.Prenom} {request.Client.Nom},</h3>
                <p>Nous vous informons que votre demande de modification de <strong>{request.FieldToModify}</strong> a été <strong>refusée</strong>.</p>
                
                <p>Date de traitement: {request.ProcessedDate?.ToString("dd/MM/yyyy HH:mm")}</p>
                <p>Pour plus d'informations, n'hésitez pas à contacter votre agence.</p>
                <p>Cordialement,<br>L'équipe de votre banque</p>";
                }

                var emailSent = await _emailService.SendEmailAsync(clientEmail, subject, message);

                if (!emailSent)
                {
                    _logger.LogError($"Échec de l'envoi de l'email de notification pour la demande {request.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de l'envoi de l'email de notification pour la demande {request.Id}");
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