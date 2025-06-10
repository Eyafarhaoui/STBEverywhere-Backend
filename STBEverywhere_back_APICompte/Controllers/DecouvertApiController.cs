using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APICompte.Repository.IRepository;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_Back_SharedModels.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_Back_SharedModels;
using System.Net.Http.Headers;
using System.Text.Json;
using STBEverywhere_back_APICompte.Services.IServices;

namespace STBEverywhere_back_APICompte.Controllers
{

    [Route("api/Decouvert")]
    [ApiController]
    public class DecouvertApiController:ControllerBase
    {

        private readonly ICompteService _compteService;
        private readonly IUserRepository _userRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DecouvertApiController> _logger;
  
        public DecouvertApiController(IUserRepository userRepository,ICompteService compteService, IHttpContextAccessor httpContextAccessor, ILogger<DecouvertApiController> logger)
        {
            _compteService = compteService;
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }




        [HttpGet("getDemandesByAgence/{agenceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDemandesByAgence(string agenceId)

        {
           
            try
            {
                var demandes = await _compteService.GetDemandesByAgenceIdAsync(agenceId);

                if (!demandes.Any())
                {
                    return NotFound(new { message = "Aucune demande trouvée pour cette agence." });
                }

                return Ok(demandes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes par agence");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur serveur" });
            }
        }




        [HttpGet("getDemandesByClient")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDemandesByClient()
        {
            try
            {
                // Récupérer l'ID du client à partir du token
                var clientId = GetUserIdFromToken();

                // Récupérer les demandes de modification de découvert pour ce client
                var demandes = await _compteService.GetDemandesByClientIdAsync(clientId);

                if (!demandes.Any())
                {
                    return NotFound(new { message = "Aucune demande trouvée pour ce client." });
                }

                return Ok(demandes);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur serveur" });
            }
        }




        /*
        [HttpPost("demandeModificationDecouvert")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DemandeModificationDecouvert([FromBody] DemandeModificationDecouvertDto demandeDto)
        {
            if (demandeDto == null || string.IsNullOrEmpty(demandeDto.RIBCompte))
            {
                return BadRequest(new { message = "Le RIB et le montant du découvert demandé sont obligatoires." });
            }

            // Vérifier si le compte existe
            var compte = await _compteService.GetByRIBAsync(demandeDto.RIBCompte);
            if (compte == null)
            {
                return NotFound(new { message = "Compte introuvable." });
            }

            // Vérifier si une demande pour ce compte est déjà en attente
            var existingDemandes = await _compteService.GetDemandesModificationAsync(demandeDto.RIBCompte,StatutDemandeEnum.EnAttente);
            if (existingDemandes.Any())
            {
                return BadRequest(new { message = "Une demande de modification de découvert est déjà en attente pour ce compte." });
            }

            // Créer une nouvelle demande
            var demande = new DemandeModificationDecouvert
            {
                RIBCompte = demandeDto.RIBCompte,
                DecouvertDemande = demandeDto.DecouvertDemande,
                StatutDemande = StatutDemandeEnum.EnAttente,
                DateDemande = DateTime.Now

            };

            await _compteService.CreateDemandeModificationAsync(demande);

            return CreatedAtAction(nameof(DemandeModificationDecouvert), new { id = demande.Id }, demande);
        }
        */

        [HttpPost("demandeModificationDecouvert")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DemandeModificationDecouvert([FromBody] DemandeModificationDecouvertDto demandeDto)
        {
            // Récupération de l'ID du client depuis le token
            var clientId = GetUserIdFromToken();

            if (demandeDto == null || string.IsNullOrEmpty(demandeDto.RIBCompte))
            {
                return BadRequest(new { message = "Le RIB et le montant du découvert demandé sont obligatoires." });
            }

            // Vérifier si le compte existe et appartient au client
            var compte = await _compteService.GetByRIBAsync(demandeDto.RIBCompte);
            if (compte == null || compte.ClientId != clientId)
            {
                return NotFound(new { message = "Compte introuvable ou n'appartenant pas au client." });
            }

            // Vérifier si une demande pour ce compte est déjà en attente
            var existingDemandes = await _compteService.GetDemandesModificationAsync(demandeDto.RIBCompte, StatutDemandeEnum.EnAttente);
            if (existingDemandes.Any())
            {
                return BadRequest(new { message = "Une demande de modification de découvert est déjà en attente pour ce compte." });
            }

            try
            {
                // Récupération du revenu mensuel
               using var httpClient = new HttpClient();
               

                // Construction de l'URL avec le paramètre userId
                var apiUrl = $"http://localhost:5260/api/Client/GetClientRevenuMensuel?userId={clientId}";

                // Ajout du token d'autorisation si nécessaire
                var token = Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(token))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));
                }

                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest(new
                    {
                        message = "Impossible de récupérer les informations du client.",
                        details = $"Statut: {response.StatusCode}, Raison: {response.ReasonPhrase}"
                    });
                }

                var content = await response.Content.ReadAsStringAsync();
                var client = JsonSerializer.Deserialize<Client>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (client == null || client.RevenuMensuel <= 0)
                {
                    return BadRequest(new { message = "Revenu mensuel non valide ou client introuvable." });
                }

                // Calcul du découvert maximum autorisé
                decimal decouvertMaxAutorise = Convert.ToDecimal(client.RevenuMensuel) * 2;

                if (demandeDto.DecouvertDemande > decouvertMaxAutorise)
                {
                    return BadRequest(new
                    {
                        message = $"Le découvert demandé ne peut pas dépasser le double de votre revenu mensuel.",
                        maxAutorise = decouvertMaxAutorise,
                        revenuMensuel = client.RevenuMensuel
                    });
                }

                // Création de la demande
                var demande = new DemandeModificationDecouvert
                {
                    RIBCompte = demandeDto.RIBCompte,
                    DecouvertDemande = demandeDto.DecouvertDemande,
                    StatutDemande = StatutDemandeEnum.EnAttente,
                    DateDemande = DateTime.Now,
                   
                };

                await _compteService.CreateDemandeModificationAsync(demande);

                return CreatedAtAction(
                    nameof(DemandeModificationDecouvert),
                    new { id = demande.Id },
                    new
                    {
                        demande,
                        clientInfo = new
                        {
                            client.Nom,
                            client.Prenom,
                            client.RevenuMensuel
                        }
                    });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erreur HTTP lors de la communication avec le service Client");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Service Client indisponible",
                    details = ex.Message
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erreur de désérialisation");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Erreur de traitement des données client",
                    details = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur serveur inattendue");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Erreur interne du serveur",
                    details = ex.Message
                });
            }
        }



        [HttpGet("getDecouvertAutorise/{rib}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDecouvertAutorise(string rib)
        {
            if (string.IsNullOrEmpty(rib))
            {
                return BadRequest(new { message = "Le RIB est obligatoire." });
            }

            // Vérifier si le compte existe
            var compte = await _compteService.GetByRIBAsync(rib);
            if (compte == null)
            {
                return NotFound(new { message = "Compte introuvable." });
            }

            return Ok(new { DecouvertAutorise = compte.DecouvertAutorise });
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


























    }
}
