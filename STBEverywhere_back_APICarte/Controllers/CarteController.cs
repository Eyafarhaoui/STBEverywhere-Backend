using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APICarte.Services;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models.DTO;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.Json;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_back_APICarte.Repository;
using STBEverywhere_Back_SharedModels.Models.enums;
using System.IdentityModel.Tokens.Jwt;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_Back_SharedModels.Models;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.JsonPatch.Operations;
using System.Globalization;
using RestSharp;

namespace STBEverywhere_back_APICarte.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarteController : ControllerBase

    {
        private readonly ICarteService _carteService;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CarteService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly ICvvGeneratorService _cvvGeneratorService;
        private readonly ICarteRepository _carteRepository;


        public CarteController(ICarteService carteService, ICarteRepository carteRepository, ICvvGeneratorService cvvGeneratorService,
            IHttpContextAccessor httpContextAccessor, IUserRepository userRepository, HttpClient httpClient, ILogger<CarteService> logger, ApplicationDbContext dbContext)
        {
            _carteService = carteService;
            _cvvGeneratorService = cvvGeneratorService;
            _httpClient = httpClient;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
            _userRepository = userRepository;
            _carteRepository = carteRepository;
        }

        [HttpGet("generate/{numCarte}")]
       
        public async Task<IActionResult> GenerateCvv(string numCarte)
        {
            try
            {
                var carte = await _carteRepository.GetCarteByNumCarteAsync(numCarte);
                if (carte == null)
                    return NotFound("Carte introuvable");

                var cvv = _cvvGeneratorService.GenerateSecureCvv(carte.NumCarte, carte.DateExpiration);

                _logger.LogInformation("CVV généré pour la carte {NumCarte}", numCarte);

                return Ok(new
                {
                    NumCarte = numCarte,
                    Cvv = cvv,
                    ExpirationDate = carte.DateExpiration.ToString("MM/yy")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération du CVV");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }

        //API pour recuperer les Carte par RIB Compte se sont les Cartes prepayee
        [HttpGet("rib/{rib}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<CarteDTO>>> GetCartesByRIB(string rib)
        {




            var cartes = await _carteService.GetCartesByRIBAsync(rib);
            return Ok(cartes);



        }


        private readonly Dictionary<NomCarte, decimal> _fraisCartes = new()
{
    { NomCarte.VisaClassic, 35 },
    { NomCarte.Mastercard, 35 },
    { NomCarte.Tecno, 15 },
    { NomCarte.VisaPlatinum, 150 },
    { NomCarte.VisaInfinite, 200 },
    { NomCarte.MastercardGold, 90 },
    { NomCarte.CIB, 20 },
    { NomCarte.Epargne, 0 }
};
        [HttpPost("demande")]

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> CreateDemandeCarte(DemandeCarteDTO demandeCarteDTO)
        {
            // Récupérer le ClientId depuis le token

            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;

            try
            {
                _logger.LogInformation("Création d'une nouvelle demande de carte pour le compte : {NumCompte}", demandeCarteDTO.NumCompte);

                // Appeler l'API du service de compte pour vérifier si le RIB existe
                var response = await _httpClient.GetAsync($"http://localhost:5185/api/compte/GetByRIB/{demandeCarteDTO.NumCompte}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RIB invalide : {NumCompte}", demandeCarteDTO.NumCompte);
                    return BadRequest(new { success = false, message = "RIB invalide." });
                }



                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Réponse de l'API Compte : {JsonResponse}", jsonResponse);

                // Désérialiser la réponse en une liste de Compte
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var comptes = JsonSerializer.Deserialize<List<Compte>>(jsonResponse, options);
                if (comptes == null || !comptes.Any())
                {
                    _logger.LogWarning("Compte introuvable : {NumCompte}", demandeCarteDTO.NumCompte);
                    return NotFound(new { success = false, message = "Le compte associé n'existe pas." });
                }

                // Extraire le premier compte de la liste
                var compte = comptes.First();

                //verification de solde
                // Après la vérification du type de compte épargne...

                // Vérification du solde et découvert
                var fraisCarte = _fraisCartes[demandeCarteDTO.NomCarte];

                if (fraisCarte > 0 && (compte.Solde + compte.DecouvertAutorise) < fraisCarte)
                {
                    _logger.LogWarning("Solde insuffisant pour la carte {NomCarte}. Requis: {Frais}, Disponible: {Disponible}",
                        demandeCarteDTO.NomCarte,
                        fraisCarte,
                        compte.Solde + compte.DecouvertAutorise);

                    return BadRequest(new
                    {
                        success = false,
                        message = $"Fonds insuffisants. Frais: {fraisCarte.ToString("N2")} DT | " +
             $"Disponible: {(compte.Solde + compte.DecouvertAutorise.Value).ToString("N2")} DT"
                    });
                }


                // Continuer avec les autres vérifications...

                //une carte par compte de meme type et statut active


                var carteActiveExistante = await _carteRepository.GetCarteActiveByRIBAndNomAndTypeAsync(
                     demandeCarteDTO.NumCompte,
                     demandeCarteDTO.NomCarte,
                     demandeCarteDTO.TypeCarte
);

                if (carteActiveExistante != null)
                {
                    _logger.LogWarning(
                        "Le client a déjà une carte active avec le même nom et type pour le compte : {NumCompte}",
                        demandeCarteDTO.NumCompte
                    );
                    return BadRequest("Vous avez déjà une carte active avec ce nom et ce type.");
                }

                // Condition : Si le compte est de type "Épargne", la carte doit être de type "Épargne"
                if (compte.Type == "epargne" && demandeCarteDTO.NomCarte != NomCarte.Epargne)
                {
                    _logger.LogWarning("Tentative de création d'une carte non Épargne pour un compte Épargne : {NumCompte}", demandeCarteDTO.NumCompte);
                    return BadRequest("Un compte Épargne ne peut avoir qu'une  carte Épargne.");
                }

                // Vérifier si la demande est pour une carte Épargne
                if (demandeCarteDTO.NomCarte == NomCarte.Epargne)
                {
                    // Vérifier que le type de compte est "Épargne"
                    if (compte.Type != "epargne")
                    {
                        _logger.LogWarning("Tentative de création d'une carte Épargne pour un compte non Épargne : {NumCompte}", demandeCarteDTO.NumCompte);
                        return BadRequest("Une carte Épargne ne peut être demandée que pour un compte de type Épargne.");
                    }
                }

                var cartes = await _carteRepository.GetCartesByRIBAsync(demandeCarteDTO.NumCompte);


                // Nouvelle vérification: Visa Classic et Mastercard mutuellement exclusives
                var hasVisaClassic = cartes.Any(c => c.NomCarte == NomCarte.VisaClassic && c.Statut != StatutCarte.Expired);
                var hasMastercard = cartes.Any(c => c.NomCarte.ToString().StartsWith("Mastercard") && c.Statut != StatutCarte.Expired);

                if ((demandeCarteDTO.NomCarte.ToString().StartsWith("Mastercard") && hasVisaClassic) ||
                    (demandeCarteDTO.NomCarte == NomCarte.VisaClassic && hasMastercard))
                {
                    _logger.LogWarning("Conflit entre Visa Classic et Mastercard pour le compte : {NumCompte}", demandeCarteDTO.NumCompte);
                    return BadRequest("Vous ne pouvez pas avoir à la fois une Visa Classic et une Mastercard sur le même compte.");
                }
                //si le client possede visa platinum il ne peux pas demandee mastercard ou visa classique 
                var hasPlatinum = cartes.Any(c => c.NomCarte == NomCarte.VisaPlatinum && c.Statut != StatutCarte.Expired);

                if (hasPlatinum &&
                   (demandeCarteDTO.NomCarte.ToString().StartsWith("Mastercard") ||
                    demandeCarteDTO.NomCarte == NomCarte.VisaClassic))
                {
                    return BadRequest("Vous ne pouvez pas demander une carte Mastercard ou Visa Classic car vous possédez déjà une Visa Platinum.");
                }
                // Condition 1: Maximum 2 cartes internationales par compte
                if (demandeCarteDTO.TypeCarte == TypeCarte.International && cartes.Count(c => c.TypeCarte == TypeCarte.International) >= 2)
                {
                    _logger.LogWarning("Tentative de création d'une troisième carte internationale pour le compte : {NumCompte}", demandeCarteDTO.NumCompte);
                    return BadRequest("Un compte ne peut avoir que 2 cartes internationales.");
                }

                // Condition 2: Maximum 2 cartes nationales par compte
                if (demandeCarteDTO.TypeCarte == TypeCarte.National && cartes.Count(c => c.TypeCarte == TypeCarte.National) >= 2)
                {
                    _logger.LogWarning("Tentative de création d'une troisième carte nationale pour le compte : {NumCompte}", demandeCarteDTO.NumCompte);
                    return BadRequest("Un compte ne peut avoir que 2 cartes nationales.");
                }

                // Condition 3: Une seule carte épargne par compte
                if (demandeCarteDTO.NomCarte == NomCarte.Epargne && cartes.Any(c => c.NomCarte == NomCarte.Epargne))
                {
                    _logger.LogWarning("Tentative de création d'une deuxième carte épargne pour le compte : {NumCompte}", demandeCarteDTO.NumCompte);
                    return BadRequest("Un compte ne peut avoir qu'une seule carte épargne.");
                }

                // Condition 4: Pas de demande en cours avec le même nom et type de carte
                var demandesExistantes = await _carteRepository.GetDemandesByCompteAndNomAndTypeAsync(
                    demandeCarteDTO.NumCompte,
                    demandeCarteDTO.NomCarte,
                    demandeCarteDTO.TypeCarte
                );

                if (demandesExistantes.Any(d => d.Statut != StatutDemande.Recuperee))
                {
                    _logger.LogWarning("Une demande existe déjà avec le même nom et type de carte pour le compte : {NumCompte}", demandeCarteDTO.NumCompte);
                    return BadRequest("Une demande est déjà en cours avec le même nom et type de carte.");
                }

                // Créer la demande de carte avec le statut "En cours de préparation"
                var demandeCarte = new DemandeCarte
                {
                    NumCompte = demandeCarteDTO.NumCompte,
                    NomCarte = demandeCarteDTO.NomCarte,
                    TypeCarte = demandeCarteDTO.TypeCarte,
                    CIN = demandeCarteDTO.CIN,
                    Email = demandeCarteDTO.Email,
                    NumTel = demandeCarteDTO.NumTel,
                    DateCreation = DateTime.Now, // Date de création définie sur maintenant
                    Statut = StatutDemande.EnCours, // Statut initial

                };

                // Enregistrer la demande de carte// Enregistrer la demande de carte
                _dbContext.DemandesCarte.Add(demandeCarte);
                await _dbContext.SaveChangesAsync();

                // Retourner une réponse JSON structurée
                return Ok(new { success = true, message = "Demande de carte créée avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la demande de carte.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Une erreur interne est survenue." });
            }
        }


        [HttpGet("demandes/rib/{rib}")]


        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<DemandeCarteDTO>>> GetDemandesByRIB(string rib)
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;


            var demandes = await _carteService.GetDemandesByRIBAsync(rib);
            return Ok(demandes);
        }



        [HttpGet("details/{numCarte}")]


        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CarteDTO>> GetCarteDetails(string numCarte)
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;

            try
            {
                var carteDetails = await _carteService.GetCarteDetailsAsync(numCarte);
                return Ok(carteDetails);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("block/{numCarte}")]

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> BlockCarte(string numCarte)
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;

            try
            {
                var result = await _carteService.BlockCarteAsync(numCarte);
                return Ok(new { message = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });

            }
        }


        [HttpPost("deblock/{numCarte}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeBlockCarte(string numCarte)
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;
            try
            {
                var result = await _carteService.DeBlockCarteAsync(numCarte);
                return Ok(new { message = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet("cartes/by-client")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<CarteDTO>>> GetCartesByClientId()
        {
            // Récupérer le ClientId depuis le token
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;


            try
            {
                // Récupérer les cartes associées au client
                var cartes = await _carteService.GetCartesByClientIdAsync(clientId);

                // Si aucune carte n'est trouvée, retourner une réponse 404
                if (cartes == null || !cartes.Any())
                {
                    return NotFound(new { message = "Aucune carte trouvée pour ce client." });
                }

                // Retourner les cartes trouvées
                return Ok(cartes);
            }
            catch (Exception ex)
            {
                // Journaliser l'erreur et retourner une réponse 500
                _logger.LogError(ex, "Erreur lors de la récupération des cartes par client ID.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Une erreur interne est survenue.");
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
        [HttpPost("demande-prepayee")]
        public async Task<ActionResult> CreateDemandeCartePrepayee([FromBody] DemandeCarteDTO demandeCarteDTO)
        {
            try
            {
                // Validation du modèle
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Données invalides",
                        errors = ModelState.Values.SelectMany(v => v.Errors)
                    });
                }

                // 1. Authentification et récupération du client
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                if (client == null)
                    return NotFound(new { success = false, message = "Client non trouvé" });

                // 2. Vérification des demandes existantes
                var demandeExistante = await _dbContext.DemandesCarte
                    .AnyAsync(d => d.NomCarte == demandeCarteDTO.NomCarte
                                && d.TypeCarte == demandeCarteDTO.TypeCarte
                                && d.Statut != StatutDemande.Recuperee);

                if (demandeExistante)
                    return BadRequest(new { success = false, message = "Demande similaire déjà en cours" });

                // 3. Création du compte technique
                var compteTechnique = await CreateCompteTechniqueAsync();
                if (compteTechnique == null)
                    return StatusCode(500, new { success = false, message = "Échec création compte technique" });

                // 4. Création de la demande
                var demandeCarte = new DemandeCarte
                {
                    NumCompte = compteTechnique.RIB,
                    NomCarte = demandeCarteDTO.NomCarte,
                    TypeCarte = demandeCarteDTO.TypeCarte,

                    CIN = demandeCarteDTO.CIN, // Assurez-vous que ce champ est mappé
                    Email = demandeCarteDTO.Email,
                    NumTel = demandeCarteDTO.NumTel,
                    DateCreation = DateTime.UtcNow,
                    Statut = StatutDemande.EnCours,

                };

                _dbContext.DemandesCarte.Add(demandeCarte);
                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Demande créée avec succès",
                    rib = compteTechnique.RIB
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création demande carte prépayée");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la création",
                    detail = ex.ToString() // Affiche toute la stack trace
                });
            }
        }

        private async Task<Compte> CreateCompteTechniqueAsync()
        {
            try
            {
                var httpClient = new HttpClient();

                // Récupérez le token du header Authorization de la requête originale
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    httpClient.DefaultRequestHeaders.Authorization =
                        AuthenticationHeaderValue.Parse(authHeader);
                }

                var response = await httpClient.PostAsJsonAsync(
                    "http://localhost:5185/api/compte/CreateCompteTechnique",
                    new { type = "technique" });

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Échec création compte technique. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, errorContent);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<Compte>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création du compte technique");
                return null;
            }
        }


        [HttpPost("demande-augmentation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public async Task<ActionResult> CreateDemandeAugmentation([FromBody] DemandeAugmentationPlafondDTO demandeDto)
        {
            try
            {
                // Récupération du client
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                // Vérification que la carte appartient au client
                var carte = await _dbContext.Cartes
                    .Include(c => c.Compte)
                    .FirstOrDefaultAsync(c => c.NumCarte == demandeDto.NumCarte && c.Compte.ClientId == client.Id);

                if (carte == null)
                    return Unauthorized(new { success = false, message = "Carte non trouvée ou non autorisée" });
                // Vérification s'il existe déjà une demande en cours pour cette carte
                var demandeEnCours = await _dbContext.DemandesAugmentationPlafond
                    .AnyAsync(d => d.NumCarte == demandeDto.NumCarte &&
                                   d.Statut == StatutDemandeAug.EnAttente.ToString());

                if (demandeEnCours)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Une demande d'augmentation est déjà en cours pour cette carte"
                    });
                }

                // Validation des nouveaux plafonds
                if (demandeDto.NouveauPlafondTPE <= carte.PlafondTPE && demandeDto.NouveauPlafondDAB <= carte.PlafondDAP)
                    return BadRequest(new { success = false, message = "Les nouveaux plafonds doivent être supérieurs aux actuels" });

                // Création de la demande
                var demande = new DemandeAugmentationPlafond
                {
                    NumCarte = demandeDto.NumCarte,
                    NouveauPlafondTPE = demandeDto.NouveauPlafondTPE,
                    NouveauPlafondDAB = demandeDto.NouveauPlafondDAB,
                    Raison = demandeDto.Raison,
                    DateDemande = DateTime.UtcNow,
                    Statut = StatutDemandeAug.EnAttente.ToString()
                };

                _dbContext.DemandesAugmentationPlafond.Add(demande);
                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Demande d'augmentation créée",
                    demandeId = demande.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création demande augmentation");
                return StatusCode(500, new { success = false, message = "Erreur interne" });
            }
        }

        [HttpGet("demandes-augmentation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<DemandeAugmentationPlafondDTO>>> GetDemandesAugmentation()
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);

            var demandes = await _dbContext.DemandesAugmentationPlafond
                .Include(d => d.Carte)
                .ThenInclude(c => c.Compte)
                .Where(d => d.Carte.Compte.ClientId == client.Id)
                .OrderByDescending(d => d.DateDemande)
                .Select(d => new DemandeAugmentationPlafond
                {
                    Id = d.Id,
                    NumCarte = d.NumCarte,
                    NouveauPlafondTPE = d.NouveauPlafondTPE,
                    NouveauPlafondDAB = d.NouveauPlafondDAB,
                    DateDemande = d.DateDemande,
                    Statut = d.Statut,
                    Raison = d.Raison
                })
                .ToListAsync();

            return Ok(demandes);
        }

        [HttpPost("effectuer-recharge")]
        public async Task<IActionResult> EffectuerRecharge([FromBody] RechargeCarteDto dto)
        {
            var userId = GetUserIdFromToken();
            var clientEmetteur = await _userRepository.GetClientByUserIdAsync(userId);
            var telEmetteur = clientEmetteur.Telephone;
            var DateVirement = DateTime.Now.ToString("dd/MM");
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            

            try
            {
                // 1. Récupération des cartes avec leurs comptes et clients
                var carteEmetteur = await _dbContext.Cartes
                    .AsTracking()
                    .Include(c => c.Compte)
                    .ThenInclude(c => c.Client)
                    .FirstOrDefaultAsync(c => c.NumCarte == dto.CarteEmetteurNum);
                var carteRecepteur = await _dbContext.Cartes
                    .AsTracking()
                    .Include(c => c.Compte)
                    .ThenInclude(c => c.Client)
                    .FirstOrDefaultAsync(c => c.NumCarte == dto.CarteRecepteurNum);

                if (carteEmetteur == null || carteRecepteur == null)
                    return NotFound("Une des cartes est introuvable");

                if (carteEmetteur.Compte.ClientId != clientEmetteur.Id)
                    return Unauthorized("Vous n'êtes pas autorisé à utiliser cette carte");

                // 2. Validation de la date d'expiration
                if (!DateTime.TryParseExact(dto.DateExpiration, "MM/yy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var clientExpirationDate))
                {
                    return BadRequest("Format de date invalide (MM/yy attendu)");
                }

                // Convertir en DateTime complète (dernier jour du mois)
                var serverExpirationDate = new DateTime(
                    clientExpirationDate.Year,
                    clientExpirationDate.Month,
                    DateTime.DaysInMonth(clientExpirationDate.Year, clientExpirationDate.Month),
                    23, 59, 59);

                if (carteEmetteur.DateExpiration.ToString("MM/yy") != dto.DateExpiration)
                {
                    _logger.LogWarning($"Date expiration invalide. Reçue: {dto.DateExpiration}, Attendue: {carteEmetteur.DateExpiration:MM/yy}");
                    return BadRequest("Date d'expiration incorrecte");
                }

                // 3. Validation du CVV
                bool isCvvValid = _cvvGeneratorService.ValidateCvv(
                    carteEmetteur.NumCarte,
                    serverExpirationDate,
                    dto.Cvv);

                if (!isCvvValid)
                {
                    _logger.LogWarning($"CVV invalide pour la carte {carteEmetteur.NumCarte}");
                    return BadRequest("Code CVV invalide");
                }

                // 4. Vérification du solde
                if ((carteEmetteur.Compte.Solde + carteEmetteur.Compte.DecouvertAutorise) < dto.Montant)
                {
                    _logger.LogWarning($"Solde insuffisant pour la carte {carteEmetteur.NumCarte}");
                    return BadRequest("Solde insuffisant pour effectuer la recharge");
                }

                // 5. Création de la recharge
                var recharge = new RechargeCarte
                {
                    CarteEmetteurNum = dto.CarteEmetteurNum,
                    CarteRecepteurNum = dto.CarteRecepteurNum,
                    Montant = dto.Montant,
                    DateRecharge = DateTime.UtcNow,
                    //DateExpirationUsed = dto.DateExpiration // Stockage de la date utilisée (optionnel)
                };

                _dbContext.RechargesCarte.Add(recharge);

                // 6. Mise à jour des soldes
                carteEmetteur.Compte.Solde -= dto.Montant;
                carteRecepteur.Compte.Solde += dto.Montant;
                
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();



                // SMS pour Emetteur
                var smsRequestEmetteur = new
                {
                    mobile = telEmetteur,
                    message = $"Le solde de votre compte {carteEmetteur.Compte.RIB} au {DateVirement} est {carteEmetteur.Compte.Solde} TND. Dernière opération: RETRAIT DÉPLACÉ: -{dto.Montant} TND"
                };

                // SMS pour Recepteur
                var smsRequestRecepteur = new
                {
                    mobile = carteRecepteur.Compte.Client.Telephone,
                    message = $"Le solde de votre compte {carteRecepteur.Compte.RIB} au {DateVirement} est {carteRecepteur.Compte.Solde} TND. Dernière opération: Recharge Carte:&#43{dto.Montant} TND"

                };

                var options = new RestClientOptions("http://localhost:5203")
                {
                    MaxTimeout = -1,
                };
                var clientrest = new RestClient(options);

                var requestEmetteur = new RestRequest("/Send", Method.Post);
                requestEmetteur.AddHeader("Content-Type", "application/json");
                requestEmetteur.AddJsonBody(smsRequestEmetteur);
                RestResponse responseEmetteur = await clientrest.ExecuteAsync(requestEmetteur);

                var requestRecepteur = new RestRequest("/Send", Method.Post);
                requestRecepteur.AddHeader("Content-Type", "application/json");
                requestRecepteur.AddJsonBody(smsRequestRecepteur);
                RestResponse responseRecepteur = await clientrest.ExecuteAsync(requestRecepteur);

                if (responseEmetteur.IsSuccessful && responseRecepteur.IsSuccessful)
                {
                    return Ok(new { message = "Virement effectué et SMS envoyés avec succès." });
                }
                else
                {
                    return StatusCode(500, new { error = "Erreur lors de l'envoi des SMS." });
                }
                // 7. Préparation de la réponse

                return Ok(new
                {
                    Success = true,
                    Message = "Recharge effectuée avec succès",
                    Data = new
                    {
                        RechargeId = recharge.Id,
                        MontantTransfere = dto.Montant,
                        FraisAppliques = 0,
                        NouveauSoldeEmetteur = carteEmetteur.Compte.Solde,
                        NouveauSoldeRecepteur = carteRecepteur.Compte.Solde,
                        DateExpirationValidee = dto.DateExpiration
                    }
                }); 




               




            } 
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la recharge");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Erreur interne du serveur",
                    Details = ex.Message
                });
            }
        }
        [HttpGet("historique-recharges")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public async Task<ActionResult<IEnumerable<HistoriqueRechargeDto>>> GetHistoriqueRecharges()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                var cartesClient = await _dbContext.Cartes
                    .Where(c => c.Compte.ClientId == client.Id)
                    .Select(c => c.NumCarte)
                    .ToListAsync();

                var historique = await _dbContext.RechargesCarte
                    .Include(r => r.CarteEmetteur)
                    .Include(r => r.CarteRecepteur)
                    .Where(r => cartesClient.Contains(r.CarteEmetteurNum) ||
                                cartesClient.Contains(r.CarteRecepteurNum))
                    .OrderByDescending(r => r.DateRecharge)
                    .Select(r => new HistoriqueRechargeDto
                    {
                        Id = r.Id,
                        DateRecharge = r.DateRecharge,
                        CarteEmetteurNum = r.CarteEmetteurNum,
                        CarteRecepteurNum = r.CarteRecepteurNum,
                        Montant = r.Montant,
                        Frais = _dbContext.FraisCartes
                    .Where(f => f.IdsRechargesStr.Contains(r.Id.ToString()))
                    .Sum(f => f.Montant),

                        isDebit = cartesClient.Contains(r.CarteEmetteurNum) 
                    })
                    .ToListAsync();

                return Ok(historique);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de l'historique des recharges");
                return StatusCode(StatusCodes.Status500InternalServerError, "Erreur interne du serveur");
            }
        }


        [HttpGet("getDemandesPlafondByAgence/{agenceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        public async Task<IActionResult> GetDemandesByAgence(string agenceId)
        {
            try
            {


                var demandes = await _carteService.GetDemandesPlafondByAgenceIdAsync(agenceId);

                if (!demandes.Any())
                {
                    return NotFound(new { message = "Aucune demande trouvée pour cette agence." });
                }

                // Mapper vers un DTO si nécessaire
                var result = demandes.Select(d => new {
                    d.Id,
                    d.NumCarte,
                    d.NouveauPlafondTPE,
                    d.NouveauPlafondDAB,
                    d.Raison,
                    d.DateDemande,
                    d.Statut,
                    ClientNom = d.Carte?.Compte?.Client?.Nom,
                    ClientPrenom = d.Carte?.Compte?.Client?.Prenom
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes par agence");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur serveur" });
            }
        }


        [HttpPost("repondre-demande-augmentation")]

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RepondreDemandeAugmentation([FromBody] ReponseDemandeAugmentationPlafondDto dto)
        {
            try
            {


                // Récupérer la demande pour vérifier l'agence
                var demande = await _dbContext.DemandesAugmentationPlafond
                    .Include(d => d.Carte)
                    .ThenInclude(c => c.Compte)
                    .ThenInclude(c => c.Client)
                    .FirstOrDefaultAsync(d => d.Id == dto.DemandeId);

                if (demande == null)
                    return NotFound(new { message = "Demande introuvable" });


                // Traiter la demande
                var result = await _carteService.RepondreDemandeAugmentationAsync(
                    dto.DemandeId,
                    dto.NouveauStatut,
                    dto.Commentaire);

                if (!result)
                    return BadRequest(new { message = "Échec du traitement de la demande" });

                return Ok(new
                {
                    success = true,
                    message = "Demande traitée avec succès",
                    nouveauPlafondTPE = demande.NouveauPlafondTPE,
                    nouveauPlafondDAB = demande.NouveauPlafondDAB
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du traitement de la demande");
                return StatusCode(500, new { message = "Erreur interne du serveur" });
            }
        }

        [HttpGet("demandes-carte/by-agence/{agenceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDemandesCarteByAgence(string agenceId)
        {
            try
            {
                // Récupérer tous les clients de l'agence
                var clientsAgence = await _dbContext.Clients
                    .Where(c => c.AgenceId == agenceId)
                    .ToListAsync();

                if (!clientsAgence.Any())
                {
                    return NotFound(new { message = "Aucun client trouvé pour cette agence." });
                }

                // Récupérer les RIB des comptes de ces clients
                var ribComptes = await _dbContext.Comptes
                    .Where(c => clientsAgence.Select(cl => cl.Id).Contains(c.ClientId))
                    .Select(c => c.RIB)
                    .ToListAsync();

                // Récupérer les demandes de carte pour ces comptes
                var demandes = await _dbContext.DemandesCarte
                    .Include(d => d.Compte)
                    .ThenInclude(c => c.Client)
                    .Where(d => ribComptes.Contains(d.NumCompte))
                    .OrderByDescending(d => d.DateCreation)
                    .Select(d => new
                    {
                        d.Iddemande,
                        d.NumCompte,
                        ClientNom = d.Compte.Client.Nom,
                        ClientPrenom = d.Compte.Client.Prenom,
                        d.NomCarte,
                        d.TypeCarte,
                        d.DateCreation,
                        d.Statut,
                        d.Email,
                        d.NumTel
                    })
                    .ToListAsync();

                if (!demandes.Any())
                {
                    return NotFound(new { message = "Aucune demande de carte trouvée pour cette agence." });
                }

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
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                // Récupérer la demande avec les relations nécessaires
                var demande = await _dbContext.DemandesCarte
                    .Include(d => d.Compte)
                    .ThenInclude(c => c.Client)
                    .FirstOrDefaultAsync(d => d.Iddemande == demandeId);

                if (demande == null)
                {
                    return NotFound(new { message = "Demande de carte introuvable." });
                }

                // Valider que le nouveau statut est différent de l'actuel
                if (demande.Statut == updateDto.NouveauStatut)
                {
                    return BadRequest(new { message = "Le nouveau statut doit être différent du statut actuel." });
                }

                // Valider la transition de statut
                if (!IsValidStatusTransition(demande.Statut, updateDto.NouveauStatut))
                {
                    var allowedStatuses = GetAllowedStatusTransitions(demande.Statut);
                    return BadRequest(new
                    {
                        message = "Transition de statut invalide.",
                        statutActuel = demande.Statut,
                        statutsAutorises = allowedStatuses
                    });
                }

                // Mettre à jour le statut
                var ancienStatut = demande.Statut;
                demande.Statut = updateDto.NouveauStatut;



                // Sauvegarder les changements
                _dbContext.DemandesCarte.Update(demande);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                // Journaliser la modification
                _logger.LogInformation($"Statut demande {demandeId} changé de {ancienStatut} à {updateDto.NouveauStatut}");

                return Ok(new
                {
                    success = true,
                    message = "Statut de la demande mis à jour avec succès.",
                    demandeId = demande.Iddemande,
                    ancienStatut,
                    nouveauStatut = demande.Statut
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Erreur lors de la mise à jour du statut de la demande {demandeId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur serveur lors de la mise à jour du statut." });
            }
        }

        // Valide les transitions de statut possibles
        private bool IsValidStatusTransition(StatutDemande ancienStatut, StatutDemande nouveauStatut)
        {
            switch (ancienStatut)
            {
                case StatutDemande.EnCours:
                    return nouveauStatut == StatutDemande.EnPreparation ||
                           nouveauStatut == StatutDemande.DisponibleEnAgence ||
                           nouveauStatut == StatutDemande.Recuperee ||
                           nouveauStatut == StatutDemande.Rejetee;

                case StatutDemande.EnPreparation:
                    return nouveauStatut == StatutDemande.DisponibleEnAgence ||
                         nouveauStatut == StatutDemande.Recuperee ||
                         nouveauStatut == StatutDemande.Livree ||
                         nouveauStatut == StatutDemande.Rejetee;

                case StatutDemande.DisponibleEnAgence:
                    return nouveauStatut == StatutDemande.Livree ||
                           nouveauStatut == StatutDemande.Recuperee ||
                           nouveauStatut == StatutDemande.Rejetee;

                case StatutDemande.Livree:
                    return nouveauStatut == StatutDemande.Recuperee;


                case StatutDemande.Rejetee:
                    return false; // Statuts terminaux

                default:
                    return false;
            }
        }

        // Retourne les statuts possibles pour une transition
        private List<StatutDemande> GetAllowedStatusTransitions(StatutDemande currentStatus)
        {
            return currentStatus switch
            {
                StatutDemande.EnCours => new List<StatutDemande> {
            StatutDemande.EnPreparation,
            StatutDemande.DisponibleEnAgence,
            StatutDemande.Recuperee,
            StatutDemande.Rejetee
        },
                StatutDemande.EnPreparation => new List<StatutDemande> {
            StatutDemande.DisponibleEnAgence,
            StatutDemande.Recuperee,
            StatutDemande.Rejetee
        },
                StatutDemande.DisponibleEnAgence => new List<StatutDemande> {
            StatutDemande.Livree,
            StatutDemande.Recuperee,
            StatutDemande.Rejetee
        },
                StatutDemande.Livree => new List<StatutDemande> {
            StatutDemande.Recuperee
        },
                _ => new List<StatutDemande>(),
            };
        }








        [HttpGet("frais-cartes/by-rib/{rib}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFraisCartesByRib(string rib)
        {
            try
            {
                // Étape 1 : récupérer toutes les cartes associées à ce RIB
                var cartes = await _carteRepository.GetCartesByRIBAsync(rib);

                if (cartes == null || !cartes.Any())
                    return NotFound($"Aucune carte trouvée pour le RIB : {rib}");

                // Étape 2 : récupérer les frais liés à chaque carte
                var fraisCartes = new List<FraisCarte>();

                foreach (var carte in cartes)
                {
                    var frais = await _carteRepository.GetFraisByNumCarteAsync(carte.NumCarte);
                    if (frais != null && frais.Any())
                        fraisCartes.AddRange(frais);
                }

                if (!fraisCartes.Any())
                    return NotFound($"Aucun frais de carte trouvé pour le RIB : {rib}");

                return Ok(fraisCartes);
            }
            catch (Exception ex)
            {
                // Ici tu peux ajouter _logger.LogError(ex, "Erreur lors de la récupération des frais")
                return StatusCode(StatusCodes.Status500InternalServerError, "Une erreur interne est survenue.");
            }
        }









    }

}

