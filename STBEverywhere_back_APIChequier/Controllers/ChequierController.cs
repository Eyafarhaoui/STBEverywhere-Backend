using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APIChequier.Repository;
using STBEverywhere_back_APIChequier.Repository.IRepositoy;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace STBEverywhere_back_APIChequier.Controllers
{
    [Route("api/ChequierApi")]
    [ApiController]
    public class ChequierController : ControllerBase
    {
        private readonly IChequierRepository _repository;
        private readonly IDemandesChequiersRepository _demandesChequiersRepository;
        private readonly IFraisChequierRepository _fraisChequierRepository;
        private readonly ILogger<ChequierController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        public ChequierController(IFraisChequierRepository fraisChequierRepository,IChequierRepository repository, IDemandesChequiersRepository demandesChequiersRepository, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository, HttpClient httpClient, ILogger<ChequierController> logger)
        {
            _repository = repository;
            _demandesChequiersRepository = demandesChequiersRepository;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _userRepository = userRepository;
            _fraisChequierRepository = fraisChequierRepository;
        }

        [HttpGet("cheques")]
      

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
      
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetChequesParClient()
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
           
            

            try
            {
                int clientId = client.Id; ;

                // Récupérer les RIBs des comptes du client
                var ribComptes = await _demandesChequiersRepository.GetRibComptesByClientId(clientId);

                if (!ribComptes.Any())
                {
                    return NotFound("Aucun compte trouvé pour ce client.");
                }

                // Récupérer les demandes de chéquiers associées aux comptes du client
                var demandes = await _demandesChequiersRepository.GetDemandesByRibComptes(ribComptes);

                if (!demandes.Any())
                {
                    return NotFound("Aucune demande de chéquier trouvée pour ce client.");
                }

                // Récupérer directement les chéquiers liés aux demandes de ce client
                var demandesIds = demandes.Select(d => d.IdDemande).ToList();
                var chequiers = await _repository.GetChequiersByDemandesIds(demandesIds);

                if (!chequiers.Any())
                {
                    return NotFound("Aucun chéquier trouvé pour ce client.");
                }

                // Formater les résultats pour le retour API
                var chequiersClient = chequiers.Select(c => new
                {
                    Status = c.Status.ToString(),
                    c.DateLivraison,
                    //NumeroChequier = demandes.First(d => d.IdDemande == c.DemandeChequierId).NumeroChequier,
                    PlafondChequier = demandes.First(d => d.IdDemande == c.DemandeChequierId).PlafondChequier,
                    RibCompte = demandes.First(d => d.IdDemande == c.DemandeChequierId).RibCompte,
                    Type = demandes.First(d => d.IdDemande == c.DemandeChequierId).isBarre ? "Barré" : "Non barré",
                    DemandeChequierId=c.DemandeChequierId,

                    //AgenceLivraison = demandes.First(d => d.IdDemande == c.DemandeChequierId).Agence // Agence de livraison
                }).ToList();

                return Ok(chequiersClient);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }







        [HttpGet("feuilles/{idDemande}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFeuillesParChequier(int idDemande)
        {
            try
            {
                // Vérifier que le client est autorisé à accéder à ce chéquier
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);
                var ribComptes = await _demandesChequiersRepository.GetRibComptesByClientId(client.Id);

                // Trouver la demande correspondant au numéro de chéquier
                var demande = await _demandesChequiersRepository.GetDemandeByChequier(idDemande);
                if (demande == null) return NotFound("Chéquier non trouvé");

                // Vérifier que le compte appartient bien au client
                if (!ribComptes.Contains(demande.RibCompte))
                    return Unauthorized("Ce chéquier ne vous appartient pas");

                // Récupérer les feuilles de la demande
                var feuilles = await _demandesChequiersRepository.GetFeuillesByDemandeId(demande.IdDemande);

                if (!feuilles.Any())
                    return NotFound("Aucune feuille trouvée pour ce chéquier");

                var feuillesDetails = feuilles.Select(f => new
                {
                    NumeroFeuille = f.NumeroFeuille,
                    PlafondFeuille = f.PlafondFeuille
                }).ToList();

                return Ok(feuillesDetails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }










        [HttpGet("cheques/{ribCompte}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetChequesParRibCompte(string ribCompte)
        {
            /*var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);*/
          

           
            try
            {
                // Récupérer les demandes de chéquiers associées au RIB du compte
                var demandes = await _demandesChequiersRepository.GetDemandesByRibCompte(ribCompte);

                if (!demandes.Any())
                {
                    return NotFound("Aucune demande de chéquier trouvée pour ce RIB.");
                }

                // Récupérer les chéquiers associés aux demandes
                var demandesIds = demandes.Select(d => d.IdDemande).ToList();
                var chequiers = await _repository.GetChequiersByDemandesIds(demandesIds);

                if (!chequiers.Any())
                {
                    return NotFound("Aucun chéquier trouvé pour ce RIB.");
                }

                // Formater les résultats pour le retour API
                var chequiersClient = chequiers.Select(c => new
                {
                    Status = c.Status.ToString(),
                    c.DateLivraison,
                    //NumeroChequier = demandes.First(d => d.IdDemande == c.DemandeChequierId).NumeroChequier,
                    PlafondChequier = demandes.First(d => d.IdDemande == c.DemandeChequierId).PlafondChequier,
                    RibCompte = demandes.First(d => d.IdDemande == c.DemandeChequierId).RibCompte,
                    Type = demandes.First(d => d.IdDemande == c.DemandeChequierId).isBarre ? "Barré" : "Non barré",
                    //AgenceLivraison = demandes.First(d => d.IdDemande == c.DemandeChequierId).Agence // Agence de livraison
                }).ToList();

                return Ok(chequiersClient);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }




        [HttpGet("frais/by-rib/{rib}")]
        [ProducesResponseType(StatusCodes.Status200OK)] // Si des frais sont trouvés
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Si aucun frais n'est trouvé
        [ProducesResponseType(StatusCodes.Status500InternalServerError)] // En cas d'erreur serveur
        public async Task<IActionResult> GetFraisByRib(string rib)
        {
            try
            {
                var frais = await _fraisChequierRepository.GetFraisChequiersByCompteAsync(rib);

                if (!frais.Any())
                    return NotFound($"Aucun frais trouvé pour le RIB : {rib}");

                return Ok(frais);
            }
            catch (Exception ex)
            {
                // Log de l'erreur possible ici (ex.Message)
                return StatusCode(StatusCodes.Status500InternalServerError, "Une erreur interne s'est produite.");
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