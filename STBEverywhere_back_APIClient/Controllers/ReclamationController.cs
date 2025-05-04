using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels.Models.enums;
using STBEverywhere_ApiAuth.Repositories;
using System.Net;
using STBEverywhere_Back_SharedModels.Data;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using SkiaSharp;
using STBEverywhere_back_APIClient.Services;
namespace STBEverywhere_back_APIClient.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
  
    public class ReclamationController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ReclamationController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ReclamationController(
            ApplicationDbContext dbContext,
            IUserRepository userRepository, IHttpContextAccessor httpContextAccessor,
            ILogger<ReclamationController> logger)
        {
            _dbContext = dbContext;
            _userRepository = userRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        
        [HttpPost("effectuer-reclamation")]
       
        public async Task<IActionResult> CreateReclamation([FromBody] ReclamationDto reclamationDto)
        {
            try
            {
                var userId = GetUserIdFromToken();
             // var client = await _userRepository.GetClientByUserIdAsync(userId);


                var client = await _userRepository.GetClientByUserIdAsync(userId);
                if (client == null)
                {
                    return BadRequest("Client not found.");
                }



                var reclamation = new Reclamation
                {
                    ClientId = client.Id,
                    Objet = reclamationDto.Objet,
                    Message = reclamationDto.Message,
                    Motif= reclamationDto.Motif,
                    Statut = ReclamationStatut.EnCours,
                    DateCreation = DateTime.UtcNow
                };

                _dbContext.Reclamations.Add(reclamation);
                await _dbContext.SaveChangesAsync();

                var reference = $"REC-{reclamation.Id.ToString().PadLeft(8, '0')}";

                return Ok(new ReclamationResponseDto
                {
                    Id = reclamation.Id,
                    Objet = reclamation.Objet,
                    Description = reclamation.Message,
                    DateCreation = reclamation.DateCreation,
                    Statut = reclamation.Statut.ToString(),
                    //Reference = reference
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la réclamation");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Une erreur est survenue");
            }
        }

        
        [HttpGet("reclamations-par-agence/{agenceId}")]
        public async Task<IActionResult> GetReclamationsParAgence(string agenceId)
        {
            try
            {
                var reclamations = await _dbContext.Reclamations
                    .Include(r => r.Client) // Inclure les données du client
                    .Where(r =>  r.Client.AgenceId == agenceId)
                    .OrderByDescending(r => r.DateCreation)
                    .Select(r => new
                    {
                        // Tous les champs de la réclamation
                        r.Id,
                        r.Objet,
                        r.Message,
                        r.Motif,
                        r.Reponse,
                        r.IdAgent,
                        r.Statut,
                        DateCreation = r.DateCreation.ToString("yyyy-MM-dd"),
                        DateResolution = r.DateResolution.HasValue ? r.DateResolution.Value.ToString("yyyy-MM-dd") : null,

                        // Informations du client
                        ClientId = r.Client.Id,
                        NomClient = r.Client.Nom + " " + r.Client.Prenom,
                        ClientCIN = r.Client.NumCIN,
                        ClientEmail = r.Client.Email,
                        ClientTelephone = r.Client.Telephone,

                        // Champ calculé
                        Reference = $"REC-{r.Id.ToString().PadLeft(8, '0')}"
                    })
                    .ToListAsync();

                return Ok(reclamations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des réclamations pour l'agence {AgenceId}", agenceId);
                return StatusCode((int)HttpStatusCode.InternalServerError, "Une erreur est survenue");
            }
        }

       



        [HttpGet("mes-reclamations")]
        public async Task<IActionResult> GetReclamations()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                var reclamations = await _dbContext.Reclamations
                    .Where(r => r.ClientId == client.Id)
                    .OrderByDescending(r => r.DateCreation)
                    .Select(r => new
                    {
                        r.Id,
                        r.Objet,
                        Description = r.Message,
                        r.DateCreation,
                        Statut = r.Statut.ToString(),
                        Reference = $"REC-{r.Id.ToString().PadLeft(8, '0')}",
                        Reponse = string.IsNullOrEmpty(r.Reponse) ? null : r.Reponse
                    })
                    .ToListAsync();

                return Ok(reclamations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des réclamations");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Une erreur est survenue");
            }
        }



     /*   [HttpGet]
        public async Task<IActionResult> GetReclamations()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                var reclamations = await _dbContext.Reclamations
                    .Where(r => r.ClientId == client.Id)
                    .OrderByDescending(r => r.DateCreation)
                    .Select(r => new ReclamationResponseDto
                    {
                        Id = r.Id,
                        Objet = r.Objet,
                        Description = r.Message,
                        Motif=r.Motif,
                        DateCreation = r.DateCreation,
                        DateResolution = r.DateResolution,
                        Statut = r.Statut.ToString(),
                       
                        Reference = $"REC-{r.Id.ToString().PadLeft(8, '0')}"
                    })
                    .ToListAsync();

                return Ok(reclamations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des réclamations");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Une erreur est survenue");
            }
        }*/

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReclamationDetails(int id)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                var reclamation = await _dbContext.Reclamations
                    .FirstOrDefaultAsync(r => r.Id == id && r.ClientId == client.Id);

                if (reclamation == null)
                    return NotFound();

                return Ok(new ReclamationResponseDto
                {
                    Id = reclamation.Id,
                    Objet = reclamation.Objet,
                    Description = reclamation.Message,
                    DateCreation = reclamation.DateCreation,
                    DateResolution = reclamation.DateResolution,
                    Statut = reclamation.Statut.ToString(),
                   // Reference = $"REC-{reclamation.Id.ToString().PadLeft(8, '0')}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de la réclamation");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Une erreur est survenue");
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
     




        [HttpGet("notifications")]
        [ProducesResponseType(StatusCodes.Status200OK)]

        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNotifications([FromServices] INotificationService notificationService)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);



                var notifications = await notificationService.GetClientReclamationNotifications(client.Id);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des notifications");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }

        [HttpPost("notifications/{notificationId}/mark-as-read")]
        [ProducesResponseType(StatusCodes.Status200OK)]

        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MarkNotificationAsRead(int notificationId, [FromServices] INotificationService notificationService)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);


                // Vérifier que la notification appartient bien au client
                var notification = await _dbContext.NotificationsReclamation
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.ClientId == client.Id);

                if (notification == null)
                {
                    return NotFound();
                }

                await notificationService.MarkReclamationNotificationAsRead(notificationId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage de la notification comme lue");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }
    }
}
