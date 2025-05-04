using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIChequier.Services;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_Back_SharedModels.Models;
using Microsoft.AspNetCore.Mvc;
using STBEverywhere_back_APIChequier.Repository.IRepositoy;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;
using STBEverywhere_ApiAuth.Repositories;
using System.IdentityModel.Tokens.Jwt;
using Org.BouncyCastle.Crypto;
using STBEverywhere_Back_SharedModels;
using System.Text;
using STBEverywhere_back_APIChequier.Repository;
using Newtonsoft.Json;
namespace STBEverywhere_back_APIChequier.Controllers
{
    [Route("api/DemandeChequierApi")]
    [ApiController]
    public class DemandeChequierController : ControllerBase
    {
        private readonly DemandeChequierService _DemandeChequierService;
        private readonly EmailService _emailService;
        //private readonly ILogger<DemandeChequierController> _logger;
        private readonly IDemandesChequiersRepository _repository;
        private readonly IFraisChequierRepository _fraisRepository;
        private readonly HttpClient _httpClient;
        private readonly ILogger<DemandeChequierController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly IDemandesChequiersRepository _DemandesChequiersRepository;
        private readonly IChequierRepository _ChequiersRepository;


        public DemandeChequierController(IFraisChequierRepository fraisRepository, IChequierRepository ChequiersRepository, IDemandesChequiersRepository DemandesChequiersRepository ,DemandeChequierService DemandeChequierService,ILogger<DemandeChequierController> logger,HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository, IDemandesChequiersRepository repository,EmailService emailService)
        {
            _logger = logger;
            _DemandeChequierService = DemandeChequierService;
            _httpClient = httpClient;
            _DemandesChequiersRepository = DemandesChequiersRepository;
            _repository = repository;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
            _ChequiersRepository = ChequiersRepository;
            _fraisRepository = fraisRepository;


            _userRepository = userRepository;
        }





        [HttpGet("getDemandesChequierById/{idDemande}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDemandeById(int idDemande)
        {
            try
            {
                _logger.LogInformation("Recherche de la demande de chéquier avec ID: {IdDemande}", idDemande);

                var demande = await _DemandesChequiersRepository.GetByIdAsync(idDemande);

                if (demande == null)
                {
                    _logger.LogWarning("Aucune demande trouvée avec l'ID: {IdDemande}", idDemande);
                    return NotFound(new { message = "Demande non trouvée." });
                }

                // Mapper les données comme dans GetDemandesParClient
                var result = new
                {
                    demande.IdDemande,
                    demande.RibCompte,
                    demande.Compte,
                    demande.DateDemande,
                    demande.NombreFeuilles,
                    Status = demande.Status.ToString(), // Convertir l'enum en string
                    demande.Otp,
                    ModeLivraison = demande.ModeLivraison.ToString(), // Convertir aussi ModeLivraison si nécessaire
                    demande.AdresseComplete,
                    demande.CodePostal,
                    demande.Email,
                    demande.NumTel,
                    //demande.NumeroChequier,
                    demande.PlafondChequier,
                    demande.RaisonDemande,
                    demande.AccepteEngagement,
                    Type = demande.isBarre ? "chéque barré" : "chéque non barrée",
                    demande.IdAgent,
                    demande.Feuilles
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de la demande avec ID: {IdDemande}", idDemande);
                return StatusCode(500, new { message = "Erreur interne du serveur", erreur = ex.Message });
            }
        }




        [HttpGet("getDemandesChequierByAgence/{agenceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        public async Task<IActionResult> GetDemandesChequierByAgence(string AgenceId)
        {
            try
            {
                _logger.LogInformation("Appel de GetDemandesChequierByAgence avec AgenceId: {AgenceId}", AgenceId);

                var result = await _DemandeChequierService.GetDemandesChequierByAgenceIdAsync(AgenceId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans GetDemandesChequierByAgence : {Message}", ex.Message);
                return StatusCode(500, new { message = "Erreur interne du serveur", erreur = ex.Message });
            }
        }





        [HttpPost("update-statut")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateStatutDemande(DemandeStatus NouveauStatut, int IdDemande, int IdAgent)
        {
            try
            {
                // Récupérer la demande à partir de son identifiant
                var demande = await _DemandesChequiersRepository.GetByIdAsync(IdDemande);

                if (demande == null)
                {
                    return NotFound("Demande introuvable.");
                }

                // Vérifier les règles de transition de statut en fonction du mode de livraison
                if (demande.ModeLivraison == ModeLivraison.LivraisonAgence &&
                    (NouveauStatut != DemandeStatus.DisponibleEnAgence && NouveauStatut != DemandeStatus.RemisAuClient))
                {
                    return BadRequest("Statut non valide pour une livraison en agence.");
                }

                if (demande.ModeLivraison == ModeLivraison.EnvoiRecommande &&
                    NouveauStatut != DemandeStatus.Expedie)
                {
                    return BadRequest("Statut non valide pour un envoi recommandé.");
                }

                demande.Status = NouveauStatut;
                demande.IdAgent = IdAgent;
                demande.DateTraitement = DateTime.Now;
                await _DemandesChequiersRepository.UpdateAsync(demande);




                // Création du chéquier uniquement si statut = RemisAuClient ou Expedie
                if (NouveauStatut == DemandeStatus.RemisAuClient || NouveauStatut == DemandeStatus.Expedie)
                {
                    var existingChequier = await _ChequiersRepository.GetByDemandeIdAsync(demande.IdDemande);
                    if (existingChequier == null)
                    {
                        var chequier = new Chequier
                        {
                            DemandeChequierId = demande.IdDemande,
                            Status = ChequierStatus.Actif,
                            DateLivraison = DateTime.Now,
                            IdAgent = IdAgent
                        };

                        await _ChequiersRepository.AddAsync(chequier);
                        await _ChequiersRepository.SaveAsync();
                    }
                }




                return Ok(new { message = $"Demande {IdDemande} mise à jour avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise à jour du statut");
                return StatusCode(500, new { error = $"Erreur interne : {ex.Message}" });
            }
        }
        [HttpPost("DemandeChequierBarre")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DemanderChequierBarre([FromBody] DemandeChequierDTO demandeDto)
        {
            if (demandeDto == null || string.IsNullOrWhiteSpace(demandeDto.RibCompte))
            {
                return BadRequest(new
                {
                    message = "Les informations de la demande sont invalides."
                });
            }

            // Vérifier qu’il n’y a pas déjà une demande en cours
            bool hasPendingRequest = await _repository.HasDemandeEncours(demandeDto.RibCompte);
            if (hasPendingRequest)
            {
                return BadRequest(new
                {
                    message = "Une demande de chéquier est déjà en cours pour ce compte."
                });
            }

            // Récupération du compte via appel HTTP
            var response = await _httpClient.GetAsync($"http://localhost:5185/api/compte/GetByRIB/{demandeDto.RibCompte}");
            if (!response.IsSuccessStatusCode)
            {
                return NotFound("Compte introuvable via le service Compte.");
            }

            var content = await response.Content.ReadAsStringAsync();
            var comptes = JsonConvert.DeserializeObject<List<Compte>>(content);
            var compte = comptes.FirstOrDefault();

            if (compte == null)
            {
                return NotFound("Compte non trouvé.");
            }

            // Vérification du type du compte
            if (compte.Type.ToLower().Contains("epargne"))
            {
                return BadRequest(new
                {
                    message = "Les comptes de type épargne ne peuvent pas faire de demande de chéquier."
                });
            }

            // Calcul des frais
            decimal fraisChequier = 5.000m;
            decimal fraisEnvoi = demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande ? 1.200m : 0m;
            decimal totalFrais = fraisChequier + fraisEnvoi;

            if (compte.Solde < totalFrais)
            {
                return BadRequest(new
                {
                    message = "Solde insuffisant pour couvrir les frais."
                });
            }

            // Débit du compte via appel PUT
            //F3 Formate le montant avec 3 décimales et remplace la virgule par un point pour l'URL

            var debitResponse = await _httpClient.PutAsync(
                $"http://localhost:5185/api/compte/Debiter/{demandeDto.RibCompte}/{totalFrais.ToString("F3").Replace(",", ".")}",
                null);

            if (!debitResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)debitResponse.StatusCode, "Échec du débit des frais via le service Compte.");
            }
            if (demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande)
            {
                if (string.IsNullOrWhiteSpace(demandeDto.AdresseComplete) || string.IsNullOrWhiteSpace(demandeDto.CodePostal))
                {
                    return BadRequest(new
                    {
                        message = "L'adresse complète et le code postal sont obligatoires pour un envoi recommandé."
                    });
                }
            }


            int totalFeuillesEmises = await _repository.CountFeuillesByRib(demandeDto.RibCompte);

            // Création de la demande
            var demande = new DemandeChequier
            {
                RibCompte = demandeDto.RibCompte,
                NombreFeuilles = demandeDto.NombreFeuilles,
                Otp = demandeDto.Otp,
                //Agence = demandeDto.Agence,
                // Agence = demandeDto.ModeLivraison == ModeLivraison.LivraisonAgence ? demandeDto.Agence : null,
                AdresseComplete = demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande ? demandeDto.AdresseComplete : null,
                CodePostal = demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande ? demandeDto.CodePostal : null,
                Email = demandeDto.Email,
                NumTel = demandeDto.NumTel,
                PlafondChequier = demandeDto.PlafondChequier,
                Status = DemandeStatus.EnCoursPreparation,
                DateDemande = DateTime.Now,
                isBarre = true,
                AccepteEngagement = null,
                RaisonDemande = null,
                ModeLivraison = demandeDto.ModeLivraison
            };
            // Feuilles
            decimal plafondFeuille = Math.Round(demandeDto.PlafondChequier / demandeDto.NombreFeuilles, 2);
            decimal correction = demandeDto.PlafondChequier - (plafondFeuille * demandeDto.NombreFeuilles);

            for (int i = 0; i < demandeDto.NombreFeuilles; i++)
            {
                decimal montantFeuille = plafondFeuille;
                if (i == demandeDto.NombreFeuilles - 1)
                    montantFeuille += correction;

                int numFeuille = totalFeuillesEmises + i + 1;
                string numeroFeuille = numFeuille.ToString("D7") + demandeDto.RibCompte;

                demande.Feuilles.Add(new FeuilleChequier
                {
                    PlafondFeuille = montantFeuille,
                    NumeroFeuille = numeroFeuille,
                    DemandeChequier = demande
                });
            }

            await _repository.AddAsync(demande);
            await _repository.SaveAsync();

            // Ajout dans la table des frais
            var fraisList = new List<FraisChequier>
    {
        new FraisChequier
        {
            type = "Frais chéquier barré",
            Date = DateTime.UtcNow,
            Montant = fraisChequier,
            IdDemande = demande.IdDemande
        }

    };

            if (demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande)
            {
                fraisList.Add(new FraisChequier
                {
                    type = "Frais envoi recommandé",
                    Date = DateTime.UtcNow,
                    Montant = fraisEnvoi,
                    IdDemande = demande.IdDemande
                });
            }

            foreach (var frais in fraisList)
            {
                await _fraisRepository.AddAsync(frais);
            }
            await _fraisRepository.SaveAsync();
            await _emailService.SendEmailAsync(demande.Email, "Demande de chéquier non barré reçue",
               $"Votre demande a bien été enregistrée et est en cours de traitement.");
            return Ok(new { message = "Demande de chéquier enregistrée et frais débités avec succès." });

           
        }


        /*
                [HttpPost("DemandeChequierBarre")]
                [ProducesResponseType(StatusCodes.Status200OK)]
                [ProducesResponseType(StatusCodes.Status404NotFound)]
                [ProducesResponseType(StatusCodes.Status500InternalServerError)]
                public async Task<IActionResult> DemanderChequierBarre([FromBody] DemandeChequierDTO demandeDto)
                {
                    if (demandeDto == null || string.IsNullOrWhiteSpace(demandeDto.RibCompte))
                    {
                        return BadRequest("Les informations de la demande sont invalides.");
                    }

                    bool hasPendingRequest = await _repository.HasDemandeEncours(demandeDto.RibCompte);
                    if (hasPendingRequest)
                    {
                        return BadRequest("Une demande de chéquier est déjà en cours de préparation pour ce compte.");
                    }

                    bool isEpargne = await _repository.IsCompteEpargne(demandeDto.RibCompte);
                    if (isEpargne)
                    {
                        return BadRequest("Les comptes de type épargne ne peuvent pas faire de demande de chéquier.");
                    }

                    // Récupération du compte
                    var response = await _httpClient.GetAsync($"https://localhost:port/api/compte/GetByRIB/{demandeDto.RibCompte}");

                    if (!response.IsSuccessStatusCode)
                    {
                        return NotFound("Compte introuvable via le service Compte.");
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var comptes = JsonConvert.DeserializeObject<List<Compte>>(content);
                    var compte = comptes.FirstOrDefault();

                    if (compte == null)
                    {
                        return NotFound("Compte non trouvé.");
                    }


                    // Calcul des frais
                    decimal fraisChequier = 5.000m;
                    decimal fraisEnvoi = demandeDto.ModeLivraison.EnvoiRecommande ? 1.200m : 0m;
                    decimal totalFrais = fraisChequier + fraisEnvoi;

                    if (compte.Solde < totalFrais)
                    {
                        return BadRequest("Solde insuffisant pour couvrir les frais.");
                    }

                    // Débiter le compte
                    compte.Solde -= totalFrais;

                    // Créer la demande
                    var demande = new DemandeChequier
                    {
                        RibCompte = demandeDto.RibCompte,
                        DateDemande = DateTime.UtcNow
                        // autres champs à mapper
                    };
                    await _repository.AddAsync(demande);
                    await _repository.SaveAsync();

                    // Ajouter les frais
                    var fraisList = new List<FraisChequier>
            {
                new FraisChequier
                {
                    type = "Frais chéquier barre",
                    Date = DateTime.UtcNow,
                    Montant = fraisChequier,
                    IdDemande = demande.IdDemande.ToString()
                }
            };

                    if (demandeDto.ModeLivraison== ModeLivraison.EnvoiRecommande)
                    {
                        fraisList.Add(new FraisChequier
                        {
                            type = "Frais envoi recommandé",
                            Date = DateTime.UtcNow,
                            Montant = fraisEnvoi,
                            IdDemande = demande.IdDemande.ToString()
                        });
                    }

                    foreach (var frais in fraisList)
                    {
                        await _fraisRepository.AddAsync(frais); // à implémenter dans un repository
                    }

                    await _compteRepository.UpdateAsync(compte);
                    await _compteRepository.SaveAsync();

                    return Ok("Demande de chéquier enregistrée et frais débités avec succès.");
                }
                */

        [HttpPost("DemandeChequierNonBarre")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DemanderChequierNonBarre([FromBody] DemandeChequierDTO demandeDto)
        {
            if (demandeDto == null || string.IsNullOrWhiteSpace(demandeDto.RibCompte) || string.IsNullOrWhiteSpace(demandeDto.RaisonDemande))
                return BadRequest("Les informations de la demande sont invalides.");

            if ((bool)!demandeDto.AccepteEngagement)
                return BadRequest("Vous devez accepter l'engagement pour continuer.");

            bool hasPendingRequest = await _repository.HasDemandeEncours(demandeDto.RibCompte);
            if (hasPendingRequest)
                return BadRequest(new
                {
                    message = "Une demande de chéquier est déjà en cours pour ce compte."
                });
           
            // Récupération du compte via HTTP 
            var response = await _httpClient.GetAsync($"http://localhost:5185/api/compte/GetByRIB/{demandeDto.RibCompte}");
            if (!response.IsSuccessStatusCode)
                return NotFound(new
                {
                    message = "Compte introuvable via le service Compte."
                });

            var content = await response.Content.ReadAsStringAsync();
            var comptes = JsonConvert.DeserializeObject<List<Compte>>(content);
            var compte = comptes.FirstOrDefault();

            if (compte == null)
                return NotFound("Compte non trouvé.");

            if (compte.Type.ToLower().Contains("epargne"))
                return BadRequest(new
                {
                    message = "Les comptes de type épargne ne peuvent pas faire de demande de chéquier."
                });

            bool hasActiveChequier = await _repository.HasActiveChequier(demandeDto.RibCompte);
            if (hasActiveChequier)
                return BadRequest(new
                {
                    message = "Le compte a déjà un chéquier actif."
                });

            if (demandeDto.PlafondChequier > 30000)
                return BadRequest(new
                {
                    message = "Le plafond du chéquier ne peut pas dépasser 30 000 dinars."
                });

            int totalFeuillesEmises = await _repository.CountFeuillesByRib(demandeDto.RibCompte);

            // Calcul des frais
            decimal fraisChequier = 6.000m;
            decimal fraisEnvoi = demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande ? 1.200m : 0m;
            decimal totalFrais = fraisChequier + fraisEnvoi;

            if (compte.Solde < totalFrais)
                return BadRequest(new
                {
                    message = "Solde insuffisant pour couvrir les frais."
                });

            // Débit du compte via HTTP
            var debitResponse = await _httpClient.PutAsync(
                $"http://localhost:5185/api/compte/Debiter/{demandeDto.RibCompte}/{totalFrais.ToString("F3").Replace(",", ".")}",
                null);

            if (!debitResponse.IsSuccessStatusCode)
                return StatusCode((int)debitResponse.StatusCode, "Échec du débit via le service Compte.");

            // Création de la demande
            var demande = new DemandeChequier
            {
                RibCompte = demandeDto.RibCompte,
                NombreFeuilles = demandeDto.NombreFeuilles,
                Otp = demandeDto.Otp,
                Email = demandeDto.Email,
                NumTel = demandeDto.NumTel,
                PlafondChequier = demandeDto.PlafondChequier,
                RaisonDemande = demandeDto.RaisonDemande,
                Status = DemandeStatus.EnCoursPreparation,
                DateDemande = DateTime.Now,
                isBarre = false,
                ModeLivraison = demandeDto.ModeLivraison,
                AdresseComplete = demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande ? demandeDto.AdresseComplete : null,
                CodePostal = demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande ? demandeDto.CodePostal : null
            };

            // Feuilles
            decimal plafondFeuille = Math.Round(demandeDto.PlafondChequier / demandeDto.NombreFeuilles, 2);
            decimal correction = demandeDto.PlafondChequier - (plafondFeuille * demandeDto.NombreFeuilles);

            for (int i = 0; i < demandeDto.NombreFeuilles; i++)
            {
                decimal montantFeuille = plafondFeuille;
                if (i == demandeDto.NombreFeuilles - 1)
                    montantFeuille += correction;

                int numFeuille = totalFeuillesEmises + i + 1;
                string numeroFeuille = numFeuille.ToString("D7") + demandeDto.RibCompte;

                demande.Feuilles.Add(new FeuilleChequier
                {
                    PlafondFeuille = montantFeuille,
                    NumeroFeuille = numeroFeuille,
                    DemandeChequier = demande
                });
            }

            await _repository.AddAsync(demande);
            await _repository.SaveAsync();

            // Enregistrement des frais
            var fraisList = new List<FraisChequier>
    {
        new FraisChequier
        {
            type = "Frais chéquier non barré",
            Date = DateTime.UtcNow,
            Montant = fraisChequier,
            IdDemande = demande.IdDemande
        }
    };

            if (demandeDto.ModeLivraison == ModeLivraison.EnvoiRecommande)
            {
                fraisList.Add(new FraisChequier
                {
                    type = "Frais envoi recommandé",
                    Date = DateTime.UtcNow,
                    Montant = fraisEnvoi,
                    IdDemande = demande.IdDemande
                });
            }

            foreach (var frais in fraisList)
            {
                await _fraisRepository.AddAsync(frais);
            }
            await _fraisRepository.SaveAsync();


            // Email de confirmation
            await _emailService.SendEmailAsync(demande.Email, "Demande de chéquier non barré reçue",
                $"Votre demande a bien été enregistrée et est en cours de traitement.");

            return Ok(new { message = "Demande de chéquier non barré soumise avec succès.", demandeId = demande.IdDemande });

        }


        [HttpGet("by-rib")]
        public async Task<IActionResult> GetDemandesByRib(string rib)
        {
            var demandes = await _DemandesChequiersRepository.GetDemandesByRibComptes(new List<string> { rib });
            return Ok(demandes);
        }
       /* [HttpGet("by-demande")]
        public async Task<IActionResult> GetCompteByDemandes(int idDemande)
        {
            var demandes = await _DemandesChequiersRepository.GetDemandesByRibComptes(new List<string> { idDemande });
            return Ok(demandes);
        }*/

        [HttpGet("ListeDemandesParClient")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDemandesParClient()
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            
            try
            {
                int clientId = client.Id;
                // Récupérer les RIBs des comptes associés à ce client
                var ribComptes = await _repository.GetRibComptesByClientId(clientId);

                if (!ribComptes.Any())
                {
                    return NotFound("Aucun compte trouvé pour ce client.");
                }

                // Récupérer les demandes de chéquiers associées aux comptes du client
                var demandes = await _repository.GetDemandesByRibComptes(ribComptes);

                if (!demandes.Any())
                {
                    return NotFound("Aucune demande de chéquier trouvée pour ce client.");
                }
                //les demandes seront affichés de la plus récentes à la plus ancienne
                var demandesTriees = demandes.OrderByDescending(d => d.DateDemande);

                // Mapper les données à retourner
                var result = demandesTriees.Select(d => new
                {
                    d.DateDemande,
                    d.RibCompte,
                    
                    Status = d.Status.ToString(),
                    Type = d.isBarre ? "chéque barré" : "chéque non barrée",


                    d.PlafondChequier,
                    d.NombreFeuilles
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
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
    }
}