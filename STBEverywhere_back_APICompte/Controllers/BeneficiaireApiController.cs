using Microsoft.AspNetCore.Mvc;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APIClient.Services;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_Back_SharedModels.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using STBEverywhere_back_APICompte.Controllers;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APICompte.Services.IServices;

namespace STBEverywhere_back_APIClient.Controllers
{
    [Route("api/Beneficiaire")]
    [ApiController]
    public class BeneficiaireApiController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<BeneficiaireApiController> _logger;

        private readonly ICompteService _compteService;


        public BeneficiaireApiController(IUserRepository userRepository,IHttpContextAccessor httpContextAccessor, ApplicationDbContext context, ILogger<BeneficiaireApiController> logger)
        {
            _context = context;
            _logger = logger;
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;

        }

        


        [HttpPost("CreateBeneficiaire")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateBeneficiaire(CreateBeneficiaireDto CreateBenefDto)
        {
            try
            {
                // 1. Extraire le ClientId du token JWT
                int clientId = GetUserIdFromToken();

               
                // 2. Valider les données du DTO
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrEmpty(CreateBenefDto.Nom))
                {
                    return BadRequest("Le nom est obligatoires pour une personne physique.");
                }
                if (string.IsNullOrEmpty(CreateBenefDto.Prenom))
                {
                    return BadRequest("Le prénom est obligatoire pour une personne physique.");
                }

                // 4. Créer un nouveau bénéficiaire
                var beneficiaire = new Beneficiaire
                {
                    Nom = CreateBenefDto.Nom,
                    Prenom = CreateBenefDto.Prenom,
                    RIBCompte = CreateBenefDto.RIBCompte,
                    Telephone = CreateBenefDto.Telephone,
                    Email = CreateBenefDto.Email,
                    ClientId = clientId
                };

                // 5. Enregistrer dans la base de données
                _context.Beneficiaires.Add(beneficiaire);
                await _context.SaveChangesAsync();

                // 6. Retourner réponse 201
                return CreatedAtAction(nameof(CreateBeneficiaire), new { id = beneficiaire.Id }, beneficiaire);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création du bénéficiaire");
                return StatusCode(500, "Une erreur interne est survenue");
            }
        }



        [HttpGet("GetBeneficiairesByClientId")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBeneficiairesByClientId()
        {
            // 1. Extraire le ClientId du token JWT
            int clientId = GetUserIdFromToken();


            // 2. Récupérer la liste des bénéficiaires pour ce ClientId
            var beneficiaires = await _context.Beneficiaires
                .Where(b => b.ClientId == clientId)
                .ToListAsync();

            // 3. Vérifier si des bénéficiaires ont été trouvés
            if (beneficiaires == null || !beneficiaires.Any())
            {
                return NotFound("Aucun bénéficiaire trouvé pour ce client.");
            }

            // 4. Retourner la liste des bénéficiaires
            return Ok(beneficiaires);
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
