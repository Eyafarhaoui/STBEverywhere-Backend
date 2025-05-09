using Microsoft.AspNetCore.Mvc;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_back_APIClient.Services;

using System.IdentityModel.Tokens.Jwt; // Pour JwtRegisteredClaimNames


using System.IO;

using Microsoft.EntityFrameworkCore;
using STBEverywhere_Back_SharedModels.Data;
using System.Security.Claims;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_ApiAuth.Repositories;



using MailKit.Security;
using MimeKit;
using iTextSharp.text.pdf;
using iTextSharp.text;

using iTextSharp.text.pdf.draw;

using Microsoft.AspNetCore.Hosting;

using Microsoft.Extensions.Logging;

using Microsoft.AspNetCore.Authorization;

namespace STBEverywhere_back_APIClient.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Applique l'authentification à toutes les méthodes du contrôleur
    public class ClientController : ControllerBase
    {
        private readonly IClientService _clientService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClientController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly INotificationService _notificationService;

        public ClientController(
            IClientService clientService,
            IUserRepository userRepository,
            IHttpContextAccessor httpContextAccessor,
             IWebHostEnvironment environment,
            ApplicationDbContext context,
            ILogger<ClientController> logger,
            EmailService emailService,
            INotificationService notificationService)
        {
            _clientService = clientService;
            _context = context;
            _logger = logger;
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;
            _emailService = emailService;
            _environment = environment;
            _notificationService = notificationService;
        }





        [HttpGet("convention/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConventionById(int id)
        {
            try
            {
                if (id <= 0)
                {
                    _logger.LogWarning("ID de convention invalide: {Id}", id);
                    return BadRequest(new
                    {
                        Status = 400,
                        Message = "ID invalide",
                        Details = "L'ID doit être un nombre positif"
                    });
                }

                var convention = await _clientService.GetConventionByIdAsync(id);

                if (convention == null)
                {
                    _logger.LogWarning("Convention non trouvée - ID: {Id}", id);
                    return NotFound(new
                    {
                        Status = 404,
                        Message = "Convention introuvable",
                        Details = $"Aucune convention avec l'ID {id}"
                    });
                }

                _logger.LogInformation("Convention récupérée avec succès - ID: {Id}", id);
                return Ok(convention);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de la convention ID: {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = 500,
                    Message = "Erreur interne du serveur",
                    
                });
            }
        }





        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetClientById(int id)
        {
            try
            {
                var client = await _context.Clients
                    .Include(c => c.User) 
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (client == null)
                {
                    return NotFound(new { message = "Client non trouvé" });
                }

             
                return Ok(client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération du client");
                return StatusCode(500, new { message = "Erreur interne du serveur" });
            }
        }



        [HttpGet("my-agency")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetClientAgency(
    [FromServices] IHttpClientFactory httpClientFactory)
        {
            try
            {
                // 1. Authentification
                var userId = GetUserIdFromToken();



                // 2. Récupération du client
                var client = await _userRepository.GetClientByUserIdAsync(userId);
                if (client == null || string.IsNullOrEmpty(client.AgenceId))
                {
                    return NotFound(new { Message = "Client ou agence non trouvée" });
                }

                // 3. Appel HTTP au service Agence
                var httpClient = httpClientFactory.CreateClient("AgenceService");
                var response = await httpClient.GetAsync($"/api/AgenceApi/byId/{client.AgenceId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Échec de la récupération de l'agence. Status: {StatusCode}", response.StatusCode);
                    return StatusCode((int)response.StatusCode, new { Message = "Erreur lors de la récupération de l'agence" });
                }

                // 4. Traitement de la réponse
                var agence = await response.Content.ReadFromJsonAsync<AgenceDto>();
                return Ok(agence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de l'agence du client");
                return StatusCode(500, new { Message = "Erreur interne du serveur" });
            }
        }










        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                _logger.LogInformation("Tentative d'enregistrement client pour {Email}", registerDto.Email);
                var result = await _clientService.RegisterAsync(registerDto);
                return Ok(new { Message = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur d'enregistrement client");
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("GetClientRevenuMensuel")]
        public async Task<IActionResult> GetClientRevenuMensuel(int userId)
        {
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                return Ok(client);
           
        }


        // Récupérer les informations du client
        [HttpGet("me")]
        public async Task<IActionResult> GetClientInfo()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                return Ok(client);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erreur d'authentification");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur serveur");
                return StatusCode(500, new { message = "Erreur interne" });
            }
        }
       

        // Télécharger le fichier KYC

        /*
          [HttpGet("kyc/download")]
           [ProducesResponseType(StatusCodes.Status200OK)]
           [ProducesResponseType(StatusCodes.Status404NotFound)]
           [ProducesResponseType(StatusCodes.Status500InternalServerError)]
           public async Task<IActionResult> DownloadKYC()
           {
               var userId = GetUserIdFromToken();
               var client = await _userRepository.GetClientByUserIdAsync(userId);


               if (client == null)
               {
                   return NotFound(new { message = "Client non trouvé" });
               }

 <<<<<<< HEAD
             var pdfBytes = GenerateKYCReport(client);
             return File(pdfBytes, "application/pdf", $"Fiche_KYC_{client.Nom}_{client.Prenom}.pdf");
         }




               var pdfBytes = GenerateKYCReport(client);
               return File(pdfBytes, "application/pdf", $"Fiche_KYC_{client.Nom}_{client.Prenom}.pdf");
           }



           // Générer un rapport KYC au format PDF
           private byte[] GenerateKYCReport(Client client)
           {
               var pdf = PdfGenerator.GeneratePdf(GenerateKycHtml(client), (PdfSharp.PageSize)PageSize.A4);
               using (var stream = new MemoryStream())
               {
                   pdf.Save(stream, false);
                   return stream.ToArray();
               }
           }


           // Générer le HTML pour le rapport KYC
           private string GenerateKycHtml(Client client)
           {
               return $@"
           <html>
               <head>
                   <style>
                       body {{ font-family: Arial, sans-serif; margin: 20px; }}
                       h1 {{ color: #2c3e50; }}
                       .info {{ margin-bottom: 15px; }}
                       .label {{ font-weight: bold; color: #34495e; }}
                       .section {{ margin-bottom: 30px; border-bottom: 1px solid #ddd; padding-bottom: 20px; }}
                       .section h2 {{ color: #2980b9; margin-bottom: 10px; }}
                       .photo-container {{ float: right; margin-left: 20px; margin-bottom: 20px; }}
                       .photo-container img {{ width: 100px; height: auto; border-radius: 5px; border: 1px solid #ddd; }}
                   </style>
               </head>
               <body>
                   <h1>Fiche KYC - {client.Nom} {client.Prenom}</h1>

                   <!-- Informations personnelles -->
                   <div class='section'>
                       <h2>Informations personnelles</h2>
                       <div class='info'>
                           <span class='label'>Nom:</span> {client.Nom}
                       </div>
                       <div class='info'>
                           <span class='label'>Prénom:</span> {client.Prenom}
                       </div>
                       <div class='info'>
                           <span class='label'>Date de naissance:</span> {client.DateNaissance.ToShortDateString()}
                       </div>
                       <div class='info'>
                           <span class='label'>Genre:</span> {client.Genre}
                       </div>
                       <div class='info'>
                           <span class='label'>Téléphone:</span> {client.Telephone}
                       </div>
                       <div class='info'>
                           <span class='label'>Email:</span> {client.Email}
                       </div>
                       <div class='info'>
                           <span class='label'>Adresse:</span> {client.Adresse}
                       </div>
                       <div class='info'>
                           <span class='label'>Civilité:</span> {client.Civilite}
                       </div>
                       <div class='info'>
                           <span class='label'>Nationalité:</span> {client.Nationalite}
                       </div>
                       <div class='info'>
                           <span class='label'>État civil:</span> {client.EtatCivil}
                       </div>
                       <div class='info'>
                           <span class='label'>Résidence:</span> {client.Residence}
                       </div>
                       <div class='info'>
                           <span class='label'>Pays de naissance:</span> {client.PaysNaissance ?? "Non renseigné"}
                       </div>
                       <div class='info'>
                           <span class='label'>Nom de la mère:</span> {client.NomMere ?? "Non renseigné"}
                       </div>
                       <div class='info'>
                           <span class='label'>Nom du père:</span> {client.NomPere ?? "Non renseigné"}
                       </div>
                   </div>

                   <!-- Informations d'identification -->
                   <div class='section'>
                       <h2>Informations d'identification</h2>
                       <div class='info'>
                           <span class='label'>Numéro CIN:</span> {client.NumCIN ?? "Non renseigné"}
                       </div>
                       <div class='info'>
                           <span class='label'>Date de délivrance CIN:</span> {client.DateDelivranceCIN?.ToShortDateString() ?? "Non renseigné"}
                       </div>
                       <div class='info'>
                           <span class='label'>Date d'expiration CIN:</span> {client.DateExpirationCIN?.ToShortDateString() ?? "Non renseigné"}
                       </div>
                       <div class='info'>
                           <span class='label'>Lieu de délivrance CIN:</span> {client.LieuDelivranceCIN ?? "Non renseigné"}
                       </div>
                   </div>

                   <!-- Informations professionnelles -->
                   <div class='section'>
                       <h2>Informations professionnelles</h2>
                       <div class='info'>
                           <span class='label'>Profession:</span> {client.Profession ?? "Non renseigné"}
                       </div>
                       <div class='info'>
                           <span class='label'>Situation professionnelle:</span> {client.SituationProfessionnelle ?? "Non renseigné"}
                       </div>
                       <div class='info'>
                           <span class='label'>Niveau d'éducation:</span> {client.NiveauEducation ?? "Non renseigné"}
                       </div>
                       <div class='info'>
                           <span class='label'>Revenu mensuel:</span> {client.RevenuMensuel.ToString("C")}
                       </div>
                   </div>

                   <!-- Informations familiales -->
                   <div class='section'>
                       <h2>Informations familiales</h2>
                       <div class='info'>
                           <span class='label'>Nombre d'enfants:</span> {client.NombreEnfants}
                       </div>
                   </div>
               </body>
           </html>
       ";
           }*/
        [HttpGet("kyc/download")]
        public async Task<IActionResult> DownloadKYC()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                if (client == null)
                    return NotFound(new { message = "Client non trouvé" });

                // Génération du PDF avec iTextSharp
                var pdfBytes = GenerateKYCReportWithITextSharp(client);
                return File(pdfBytes, "application/pdf", $"Fiche_KYC_{client.Nom}_{client.Prenom}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération de la fiche KYC");
                return StatusCode(500, new { message = "Erreur lors de la génération du PDF" });
            }
        }

        private byte[] GenerateKYCReportWithITextSharp(Client client)
        {
            using (var memoryStream = new MemoryStream())
            {
                // 1. Configuration du document
                var document = new Document(PageSize.A4, 40, 40, 60, 40); // Marge supérieure augmentée à 60
                var writer = PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                // 2. Style des polices
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DarkGray);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.Black);
                var arabicFont = FontFactory.GetFont("c:/windows/fonts/arialuni.ttf", BaseFont.IDENTITY_H, 8);
                var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, BaseColor.Black);
                var valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, BaseColor.Black);
                var sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 13, BaseColor.Black); // ✅ Ajouté

                // 3. Entête avec logo STB
                var logoPath = Path.Combine(_environment.WebRootPath, "images", "STBlogo.jpg");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = Image.GetInstance(logoPath);
                    logo.ScaleToFit(80, 80);
                    logo.Alignment = Image.ALIGN_RIGHT;
                    document.Add(logo);
                }

                // 5. Mentions légales bilingues (Français/Arabe)
                var headerTable = new PdfPTable(2) { WidthPercentage = 100 };

                // Cellule française
                var frenchCell = new PdfPCell(new Phrase(
                    "Cette fiche d'identification client (KYC) est établie conformément\n" +
                    "aux exigences réglementaires en matière de connaissance du client\n" +
                    "et de prévention des risques financiers", headerFont))
                {
                    Border = Rectangle.NO_BORDER,
                    PaddingBottom = 10
                };
                headerTable.AddCell(frenchCell);

                document.Add(headerTable);

                // 4. Titre principal centré
                var title = new Paragraph("FICHE KYC - CONNAISSANCE DE CLIENT", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 10
                };
                document.Add(title);

                // 6. Ligne de séparation
                document.Add(new Chunk(new LineSeparator(1f, 100f, BaseColor.Black, Element.ALIGN_CENTER, -1)));

                // 7. Titre client
                var clientTitle = new Paragraph($"CLIENT: {client.Nom} {client.Prenom}", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingBefore = 15,
                    SpacingAfter = 20
                };
                document.Add(clientTitle);

                // 8. Photo du client (si disponible)
                if (!string.IsNullOrEmpty(client.PhotoClient))
                {
                    var photoPath = Path.Combine(_environment.WebRootPath, "images", client.PhotoClient);
                    if (System.IO.File.Exists(photoPath))
                    {
                        var photo = Image.GetInstance(photoPath);
                        photo.ScaleToFit(100, 100);
                        photo.Alignment = Image.ALIGN_RIGHT;
                        document.Add(photo);
                    }
                }

                // 6. Sections détaillées
                AddClientSection(document, "INFORMATIONS PERSONNELLES", sectionFont, new Dictionary<string, string>
                {
                    ["Nom"] = client.Nom,
                    ["Prénom"] = client.Prenom,
                    ["Date de naissance"] = client.DateNaissance.ToString("dd/MM/yyyy"),
                    ["Genre"] = client.Genre,
                    ["Téléphone"] = client.Telephone,
                    ["Email"] = client.Email,
                    ["Adresse"] = client.Adresse,
                    ["Civilité"] = client.Civilite,
                    ["Nationalité"] = client.Nationalite,
                    ["État civil"] = client.EtatCivil,
                    ["Résidence"] = client.Residence,
                    ["Pays de naissance"] = client.PaysNaissance ?? "Non renseigné",
                    ["Nom de la mère"] = client.NomMere ?? "Non renseigné",
                    ["Nom du père"] = client.NomPere ?? "Non renseigné"
                }, labelFont, valueFont);

                AddClientSection(document, "INFORMATIONS D'IDENTIFICATION", sectionFont, new Dictionary<string, string>
                {
                    ["Numéro CIN"] = client.NumCIN ?? "Non renseigné",
                    ["Date délivrance"] = client.DateDelivranceCIN?.ToString("dd/MM/yyyy") ?? "Non renseigné",
                    ["Date expiration"] = client.DateExpirationCIN?.ToString("dd/MM/yyyy") ?? "Non renseigné",
                    ["Lieu délivrance"] = client.LieuDelivranceCIN ?? "Non renseigné"
                }, labelFont, valueFont);

                AddClientSection(document, "INFORMATIONS PROFESSIONNELLES", sectionFont, new Dictionary<string, string>
                {
                    ["Profession"] = client.Profession ?? "Non renseigné",
                    ["Situation professionnelle"] = client.SituationProfessionnelle ?? "Non renseigné",
                    ["Niveau d'éducation"] = client.NiveauEducation ?? "Non renseigné",
                    ["Revenu mensuel"] = $"{client.RevenuMensuel:0.000} TND"
                }, labelFont, valueFont);

                AddClientSection(document, "INFORMATIONS FAMILIALES", sectionFont, new Dictionary<string, string>
                {
                    ["Nombre d'enfants"] = client.NombreEnfants.ToString()
                }, labelFont, valueFont);

                // 7. Pied de page
                var footer = new Paragraph($"Généré le {DateTime.Now:dd/MM/yyyy à HH:mm}", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10))
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(footer);

                document.Close();
                return memoryStream.ToArray();
            }
        }


        private void AddClientSection(Document document, string title, Font titleFont,
            Dictionary<string, string> data, Font labelFont, Font valueFont)
        {
            // Titre de section
            var sectionTitle = new Paragraph(title, titleFont)
            {
                SpacingBefore = 15,
                SpacingAfter = 10
            };
            document.Add(sectionTitle);

            // Tableau des données
            var table = new PdfPTable(2)
            {
                WidthPercentage = 100,
                SpacingBefore = 5,
                SpacingAfter = 10
            };

            // Configuration des cellules
            var cellPadding = 5;
            var labelCell = new PdfPCell
            {
                BackgroundColor = new BaseColor(240, 240, 240),
                Padding = cellPadding,
                BorderWidth = 0.5f
            };

            var valueCell = new PdfPCell
            {
                Padding = cellPadding,
                BorderWidth = 0.5f
            };

            // Ajout des données
            foreach (var item in data)
            {
                labelCell.Phrase = new Phrase(item.Key + ":", labelFont);
                table.AddCell(labelCell);

                valueCell.Phrase = new Phrase(item.Value, valueFont);
                table.AddCell(valueCell);
            }

            document.Add(table);
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

        // ClientController.cs
        [HttpPost("request-password-change-otp")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestPasswordChangeOTP()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "Utilisateur non trouvé." });
                }

                // Générer un code OTP (6 chiffres, valide 15 minutes)
                var otpCode = new Random().Next(100000, 999999).ToString();
                var otpExpiry = DateTime.UtcNow.AddMinutes(15);

                // Stocker le code OTP dans la base de données
                user.ResetPasswordToken = otpCode;
                user.ResetPasswordTokenExpiry = otpExpiry;
                await _context.SaveChangesAsync();

                // Envoyer le code par email
                var emailSubject = "Code de vérification pour changement de mot de passe";
                var emailBody = $"Votre code de vérification est : {otpCode}\nCe code expirera dans 15 minutes.";

                await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody);

                return Ok(new { message = "Un code de vérification a été envoyé à votre adresse email." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi du code OTP");
                return StatusCode(500, new { message = "Une erreur est survenue lors de l'envoi du code de vérification." });
            }
        }

        [HttpPost("change-password-with-otp")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePasswordWithOTP([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                // 1. Récupérer l'utilisateur
                var userId = GetUserIdFromToken();
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return Unauthorized(new { message = "Utilisateur non trouvé." });
                }

                // 2. Vérifier le code OTP
                if (user.ResetPasswordToken != changePasswordDto.OTPCode ||
                    user.ResetPasswordTokenExpiry < DateTime.UtcNow)
                {
                    return BadRequest(new { message = "Code de vérification invalide ou expiré." });
                }

                // 3. Vérifier le mot de passe actuel
                if (!_userRepository.VerifyPassword(user, changePasswordDto.CurrentPassword))
                {
                    return BadRequest(new { message = "Mot de passe actuel incorrect." });
                }

                // 4. Vérifier que les nouveaux mots de passe correspondent
                if (changePasswordDto.NewPassword != changePasswordDto.ConfirmNewPassword)
                {
                    return BadRequest(new { message = "Les nouveaux mots de passe ne correspondent pas." });
                }

                // 5. Valider la force du nouveau mot de passe
                if (changePasswordDto.NewPassword.Length < 8)
                {
                    return BadRequest(new { message = "Le mot de passe doit contenir au moins 8 caractères." });
                }

                // 6. Mettre à jour le mot de passe
                _userRepository.UpdatePassword(user, changePasswordDto.NewPassword);

                // Invalider le code OTP après utilisation
                user.ResetPasswordToken = null;
                user.ResetPasswordTokenExpiry = null;

                await _context.SaveChangesAsync();

                // Envoyer une confirmation par email
                var emailSubject = "Confirmation de changement de mot de passe";
                var emailBody = "Votre mot de passe a été changé avec succès.";

                await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody);

                return Ok(new { message = "Mot de passe changé avec succès." });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erreur d'authentification");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du changement de mot de passe");
                return StatusCode(500, new { message = "Une erreur est survenue lors du changement de mot de passe." });
            }
        }

        // ClientController.cs

        [HttpPost("upload-profile-image")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            try
            {
                // 1. Vérifier si un fichier a été envoyé
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "Aucun fichier n'a été fourni." });
                }

                // 2. Vérification de la taille du fichier (max 5MB)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(new { message = "La taille du fichier ne doit pas dépasser 5MB." });
                }

                // 3. Vérification du type de fichier
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                {
                    return BadRequest(new { message = "Seuls les fichiers JPEG, PNG et GIF sont autorisés." });
                }

                // 4. Récupérer l'ID du client à partir du token
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                if (client == null)
                {
                    return NotFound(new { message = "Client non trouvé." });
                }

                // 5. Créer un nom de fichier unique
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"profile_{client.Id}_{DateTime.Now:yyyyMMddHHmmssfff}{fileExtension}";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", fileName);

                // 6. Créer le dossier s'il n'existe pas
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // 7. Sauvegarder le fichier sur le serveur
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 8. Supprimer l'ancienne photo si elle existe
                if (!string.IsNullOrEmpty(client.PhotoClient))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", client.PhotoClient);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // 9. Mettre à jour le nom du fichier dans la base de données
                client.PhotoClient = fileName;
                await _context.SaveChangesAsync();

                return Ok(new { fileName });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erreur d'authentification");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'upload de l'image");
                return StatusCode(500, new { message = "Une erreur est survenue lors de l'upload de l'image." });
            }
        }

        [HttpDelete("remove-profile-image")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public async Task<IActionResult> RemoveProfileImage()
        {
            try
            {
                // 1. Récupérer l'ID du client à partir du token
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                if (client == null)
                {
                    return NotFound(new { message = "Client non trouvé." });
                }

                // 2. Vérifier si le client a une photo
                if (string.IsNullOrEmpty(client.PhotoClient))
                {
                    return BadRequest(new { message = "Le client n'a pas de photo de profil." });
                }

                // 3. Supprimer le fichier image
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", client.PhotoClient);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // 4. Mettre à jour la base de données
                client.PhotoClient = null;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Photo de profil supprimée avec succès." });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erreur d'authentification");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression de l'image");
                return StatusCode(500, new { message = "Une erreur est survenue lors de la suppression de l'image." });
            }
        }
      

        [HttpPost("upload-documents")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadStudentDocuments([FromForm] StudentPackDto documentsDto)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var clientstb = await _userRepository.GetClientByUserIdAsync(userId);
                var clientId = clientstb.Id;

                var client = await _context.Clients.FindAsync(clientId);
                if (client == null)
                {
                    return NotFound("Client not found");
                }
                var existingElyssaRequest = await _context.PackElyssa
            .FirstOrDefaultAsync(p => p.ClientId == clientId && p.Status == "Acceptee");

                if (existingElyssaRequest != null)
                {
                    return BadRequest("Vous êtes déjà inscrit au Pack Elyssa, vous ne pouvez pas vous inscrire au Pack Student.");
                }


                var existingRequest = await _context.PackStudents
                    .FirstOrDefaultAsync(p => p.ClientId == clientId && ( p.Status == "EnAttente"));

                if (existingRequest != null)
                {
                    return BadRequest("Vous avez déjà une demande en cours.");
                }
                var existinngRequest = await _context.PackStudents
                    .FirstOrDefaultAsync(p => p.ClientId == clientId && (p.Status == "Acceptee" ));

                if (existinngRequest != null)
                {
                    return BadRequest("Vous étes deja inscrit au pack.");
                }

                var clientUploadsPath = Path.Combine(_environment.WebRootPath, "uploads", $"client_{clientId}");
                if (!Directory.Exists(clientUploadsPath))
                {
                    Directory.CreateDirectory(clientUploadsPath);
                }

                var passportFileName = await SaveFileAndGetName(documentsDto.Document1, clientUploadsPath);
                var inscriptionFileName = await SaveFileAndGetName(documentsDto.Document2, clientUploadsPath);
                var bourseFileName = await SaveFileAndGetName(documentsDto.Document3, clientUploadsPath);
                var domicileTunisieFileName = await SaveFileAndGetName(documentsDto.Document4, clientUploadsPath);

                string? domicileFranceFileName = null;
                if (documentsDto.Document5 != null)
                {
                    domicileFranceFileName = await SaveFileAndGetName(documentsDto.Document5, clientUploadsPath);
                }

                var packStudent = new PackStudent
                {
                    PassportPath = passportFileName,
                    InscriptionPath = inscriptionFileName,
                    BoursePath = bourseFileName,
                    DomicileTunisiePath = domicileTunisieFileName,
                    DomicileFrancePath = domicileFranceFileName,
                    SelectedAgency = documentsDto.Agency,
                    SubmissionDate = DateTime.UtcNow,
                    Status = "EnAttente",
                    ClientId = clientId
                };

                _context.PackStudents.Add(packStudent);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Documents envoyés avec succès!",
                    packStudentId = packStudent.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi des documents");
                return StatusCode(500, $"Erreur lors de l'envoi des documents: {ex.Message}");
            }
        }

        [HttpPost("upload-documents-elyssa")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadElyssaDocuments([FromForm] PackElyssaDto documentsDto)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var clientstb = await _userRepository.GetClientByUserIdAsync(userId);
                var clientId = clientstb.Id;

                var client = await _context.Clients.FindAsync(clientId);
                if (client == null)
                {
                    return NotFound("Client not found");
                }


                var existingStudentRequest = await _context.PackStudents
        .FirstOrDefaultAsync(p => p.ClientId == clientId && p.Status == "Acceptee");

                if (existingStudentRequest != null)
                {
                    return BadRequest("Vous êtes déjà inscrit au Pack Student, vous ne pouvez pas vous inscrire au Pack Elyssa.");
                }
                var existingRequest = await _context.PackElyssa
                    .FirstOrDefaultAsync(p => p.ClientId == clientId && (p.Status == "EnAttente"));

                if (existingRequest != null)
                {
                    return BadRequest("Vous avez déjà une demande en cours.");
                }
                var existinngRequest = await _context.PackElyssa
                    .FirstOrDefaultAsync(p => p.ClientId == clientId && (p.Status == "Acceptee"));

                if (existinngRequest != null)
                {
                    return BadRequest("Vous étes deja inscrit au pack.");
                }

                var clientUploadsPath = Path.Combine(_environment.WebRootPath, "uploads", $"Pack_Elyssa_client_{clientId}");
                if (!Directory.Exists(clientUploadsPath))
                {
                    Directory.CreateDirectory(clientUploadsPath);
                }

                var passportFileName = await SaveFileAndGetName(documentsDto.Document1, clientUploadsPath);
                var longStayVisaFileName = documentsDto.Document2 != null
                    ? await SaveFileAndGetName(documentsDto.Document2, clientUploadsPath)
                    : null;
                var visaRegistrationFileName = documentsDto.Document3 != null
                    ? await SaveFileAndGetName(documentsDto.Document3, clientUploadsPath)
                    : null;
                var frenchResidenceFileName = await SaveFileAndGetName(documentsDto.Document4, clientUploadsPath);
                var cdiContractFileName = await SaveFileAndGetName(documentsDto.Document5, clientUploadsPath);
                var taxCertificateFileName = await SaveFileAndGetName(documentsDto.Document6, clientUploadsPath);

                var packElyssa = new PackElyssa
                {
                    PassportPath = passportFileName,
                    LongStayVisaPath = longStayVisaFileName,
                    VisaRegistrationPath = visaRegistrationFileName,
                    FrenchResidenceProofPath = frenchResidenceFileName,
                    CDIContractPath = cdiContractFileName,
                    TaxWithholdingCertificatePath = taxCertificateFileName,
                    SelectedAgency = documentsDto.Agency,
                    SubmissionDate = DateTime.UtcNow,
                    Status = "EnAttente",
                    ClientId = clientId
                };

                _context.PackElyssa.Add(packElyssa);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Documents envoyés avec succès!",
                    packElyssaId = packElyssa.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi des documents");
                return StatusCode(500, $"Erreur lors de l'envoi des documents: {ex.Message}");
            }
        }




        [HttpGet("elyssa-demands-by-agency/{agencyId}")]

        public async Task<IActionResult> GetElyssaDemandsByAgency(string? agencyId = null)
        {
            try
            {
                string? finalAgencyId = agencyId;

                var demands = await _context.PackElyssa
                  .Include(p => p.Client)
                  .Where(p => p.Client.AgenceId == finalAgencyId)
                  .OrderByDescending(p => p.SubmissionDate)
                   .ToListAsync();

                return Ok(demands);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes Pack Elyssa");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }










        [HttpGet("student-demands-by-agency/{agencyId}")]

        public async Task<IActionResult> GetStudentDemandsByAgency(string? agencyId = null)
        {
            try
            {
                string? finalAgencyId = agencyId;




                var demands = await _context.PackStudents
                    .Include(p => p.Client)
                    .Where(p => p.Client.AgenceId == finalAgencyId)
                    .OrderByDescending(p => p.SubmissionDate)
                    .ToListAsync();

                return Ok(demands);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des demandes Pack Student");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }
        [HttpPost("send-student-documents-email/{demandId}")]

        public async Task<IActionResult> SendStudentDocumentsEmail(int demandId)
        {
            try
            {
                var demand = await _context.PackStudents
             .Include(p => p.Client)
             .FirstOrDefaultAsync(p => p.Id == demandId);

                if (demand == null)
                {
                    return NotFound("Demande non trouvée");
                }

                // Mettre à jour le statut de la demande
                demand.Status = "Acceptee";
                await _context.SaveChangesAsync();


                await _notificationService.NotifyPackStatusChange(
                   demand.ClientId,
                   "Student",
                   demandId,
                   "Acceptee");

                var clientUploadsPath = Path.Combine(_environment.WebRootPath, "uploads", $"client_{demand.ClientId}");

                var attachments = new List<string>
        {
            Path.Combine(clientUploadsPath, demand.PassportPath),
            Path.Combine(clientUploadsPath, demand.InscriptionPath),
            Path.Combine(clientUploadsPath, demand.BoursePath),
            Path.Combine(clientUploadsPath, demand.DomicileTunisiePath)
        };

                if (!string.IsNullOrEmpty(demand.DomicileFrancePath))
                {
                    attachments.Add(Path.Combine(clientUploadsPath, demand.DomicileFrancePath));
                }

                var emailSubject = "Nouvelle demande Pack Student";
                var emailBody = $@"
Un client STB veut s'inscrire au pack student.

Détails de la demande:

- Nom Client: {demand.Client.Nom} {demand.Client.Prenom}
- Agence sélectionnée: {demand.SelectedAgency}
- Date de soumission: {demand.SubmissionDate.ToString("dd/MM/yyyy HH:mm")}

Les documents sont joints à cet email.";

                await _emailService.SendEmailWithAttachmentsAsync(
                    "guesmii.ikram@gmail.com",
                    emailSubject,
                    emailBody,
                    attachments);

                return Ok(new { message = "Email envoyé avec succès" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de l'email");
                return StatusCode(500, "Erreur lors de l'envoi de l'email");
            }
        }

        [HttpPost("send-elyssa-documents-email/{demandId}")]

        public async Task<IActionResult> SendElyssaDocumentsEmail(int demandId)
        {
            try
            {
                var demand = await _context.PackElyssa
                    .Include(p => p.Client)
                    .FirstOrDefaultAsync(p => p.Id == demandId);

                if (demand == null)
                {
                    return NotFound("Demande non trouvée");
                }
                // Mettre à jour le statut de la demande
                demand.Status = "Acceptee";
                await _context.SaveChangesAsync();
                // Envoyer une notification
                await _notificationService.NotifyPackStatusChange(
                    demand.ClientId,
                    "Elyssa",
                    demandId,
                    "Acceptee");


                var clientUploadsPath = Path.Combine(_environment.WebRootPath, "uploads", $"Pack_Elyssa_client_{demand.ClientId}");

                var attachments = new List<string>
        {
            Path.Combine(clientUploadsPath, demand.PassportPath),
            Path.Combine(clientUploadsPath, demand.FrenchResidenceProofPath),
            Path.Combine(clientUploadsPath, demand.CDIContractPath),
            Path.Combine(clientUploadsPath, demand.TaxWithholdingCertificatePath)
        };

                if (!string.IsNullOrEmpty(demand.LongStayVisaPath))
                {
                    attachments.Add(Path.Combine(clientUploadsPath, demand.LongStayVisaPath));
                }
                if (!string.IsNullOrEmpty(demand.VisaRegistrationPath))
                {
                    attachments.Add(Path.Combine(clientUploadsPath, demand.VisaRegistrationPath));
                }

                var emailSubject = "Nouvelle demande Pack Elyssa";
                var emailBody = $@"
Un client STB veut s'inscrire au pack Elyssa.

Détails de la demande:

- Nom Client: {demand.Client.Nom} {demand.Client.Prenom}
- Agence sélectionnée: {demand.SelectedAgency}
- Date de soumission: {demand.SubmissionDate.ToString("dd/MM/yyyy HH:mm")}

Les documents sont joints à cet email.";

                await _emailService.SendEmailWithAttachmentsAsync(
                    "guesmii.ikram@gmail.com",
                    emailSubject,
                    emailBody,
                    attachments);

                return Ok(new { message = "Email envoyé avec succès" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de l'email");
                return StatusCode(500, "Erreur lors de l'envoi de l'email");
            }
        }
        private async Task<string> SaveFileAndGetName(IFormFile file, string uploadsPath)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File is empty");
            }

            // Keep original file name and extension
            var fileName = file.FileName;
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return fileName; // Return only the file name with extension
        }










       







        [HttpPost("refuser-elyssa-documents/{demandId}")]

        public async Task<IActionResult> refuserElyssaDocumentsEmail(int demandId)
        {
            try
            {
                var demand = await _context.PackElyssa
                    .Include(p => p.Client)
                    .FirstOrDefaultAsync(p => p.Id == demandId);

                if (demand == null)
                {
                    return NotFound("Demande non trouvée");
                }
                // Mettre à jour le statut de la demande
                demand.Status = "Refusee";
                await _context.SaveChangesAsync();

                await _notificationService.NotifyPackStatusChange(
                   demand.ClientId,
                   "Elyssa",
                   demandId,
                   "Refusee");

                return Ok(new { message = "demande Refusee avec succès" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de refus de la demande");
                return StatusCode(500, "Erreur lors de  refus de la demande");
            }
        }

        [HttpPost("refuser-Student-documents/{demandId}")]

        public async Task<IActionResult> refuserStudentDocumentsEmail(int demandId)
        {
            try
            {
                var demand = await _context.PackStudents
                    .Include(p => p.Client)
                    .FirstOrDefaultAsync(p => p.Id == demandId);

                if (demand == null)
                {
                    return NotFound("Demande non trouvée");
                }
                // Mettre à jour le statut de la demande
                demand.Status = "Refusee";
                await _context.SaveChangesAsync();

                await _notificationService.NotifyPackStatusChange(
                   demand.ClientId,
                   "Student",
                   demandId,
                   "Refusee");

                return Ok(new { message = "demande Refusee avec succès" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de refus de la demande");
                return StatusCode(500, "Erreur lors de  refus de la demande");
            }
        }

       


        [HttpGet("generate-student-pdf/{demandId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateStudentPdf(int demandId)
        {
            try
            {
                var demand = await _context.PackStudents
                    .Include(p => p.Client)
                    .FirstOrDefaultAsync(p => p.Id == demandId);

                if (demand == null)
                {
                    return NotFound("Demande non trouvée");
                }

                var clientUploadsPath = Path.Combine(_environment.WebRootPath, "uploads", $"client_{demand.ClientId}");

                // Vérification de l'existence du répertoire
                if (!Directory.Exists(clientUploadsPath))
                {
                    return NotFound("Répertoire des documents introuvable");
                }

                // Construction de la liste des documents avec vérification
                var documents = new List<(string path, string title)>();
                var docDefinitions = new[]
                {
            (demand.PassportPath, "Passeport"),
            (demand.InscriptionPath, "Inscription"),
            (demand.BoursePath, "Attestation de bourse"),
            (demand.DomicileTunisiePath, "Justificatif de domicile en Tunisie"),
            (demand.DomicileFrancePath, "Justificatif de domicile en France")
        };

                foreach (var doc in docDefinitions)
                {
                    if (!string.IsNullOrEmpty(doc.Item1))
                    {
                        var fullPath = Path.Combine(clientUploadsPath, doc.Item1);
                        if (System.IO.File.Exists(fullPath))
                        {
                            documents.Add((fullPath, doc.Item2));
                        }
                        else
                        {
                            _logger.LogWarning($"Fichier introuvable: {fullPath}");
                        }
                    }
                }

                if (documents.Count == 0)
                {
                    return BadRequest("Aucun document valide trouvé");
                }

                // Génération du PDF
                var pdfBytes = await GenerateCombinedPdfSafe(documents, demand.Client, "Pack Student");

                // Vérification approfondie du PDF généré
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    throw new Exception("Le PDF généré est vide");
                }

                // Vérification de l'en-tête PDF
                if (pdfBytes.Length < 4 || !pdfBytes.Take(4).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 })) // %PDF
                {
                    _logger.LogError("Le PDF généré n'a pas un en-tête valide");
                    throw new Exception("Le PDF généré est corrompu");
                }

                // Optionnel: Sauvegarde de débogage
                // System.IO.File.WriteAllBytes("debug_output.pdf", pdfBytes);

                return File(pdfBytes, "application/pdf", $"PackStudent_Demande_{demandId}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la génération du PDF. DemandId: {demandId}");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }

        private async Task<byte[]> GenerateCombinedPdfSafe(List<(string path, string title)> documents, Client client, string packType)
        {
            var memoryStream = new MemoryStream();
            var document = new Document(PageSize.A4, 50, 50, 50, 50);
            var writer = PdfWriter.GetInstance(document, memoryStream);

            try
            {
                document.Open();

                // Ajouter l'en-tête
                AddPdfHeader(document, client, packType);

                // Traiter chaque document
                foreach (var doc in documents)
                {
                    AddDocumentSectionTitle(document, doc.title);

                    try
                    {
                        var extension = Path.GetExtension(doc.path)?.ToLower() ?? "";

                        if (extension == ".pdf")
                        {
                            await SafeMergePdf(document, writer, doc.path);
                        }
                        else if (extension == ".jpg" || extension == ".jpeg" || extension == ".png")
                        {
                            AddImageToPdf(document, doc.path);
                        }
                        else
                        {
                            AddUnsupportedTypeMessage(document, Path.GetFileName(doc.path));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Erreur avec le document {doc.path}");
                        AddErrorMessage(document, $"Erreur avec le document: {Path.GetFileName(doc.path)}");
                    }
                }

                document.Close();
                writer.Close();

                return memoryStream.ToArray();
            }
            finally
            {
                document.Dispose();
                writer.Dispose();
                memoryStream.Dispose();
            }
        }

        private async Task SafeMergePdf(Document document, PdfWriter writer, string pdfPath)
        {
            try
            {
                using (var existingFileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var reader = new PdfReader(existingFileStream);
                    try
                    {
                        var numberOfPages = reader.NumberOfPages;

                        for (int i = 1; i <= numberOfPages; i++)
                        {
                            document.NewPage();
                            var importedPage = writer.GetImportedPage(reader, i);
                            var contentByte = writer.DirectContent;
                            contentByte.AddTemplate(importedPage, 0, 0);
                        }
                    }
                    finally
                    {
                        reader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la fusion du PDF: {pdfPath}");
                throw;
            }
        }

        private void AddPdfHeader(Document document, Client client, string packType)
        {
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.Blue);
            var infoFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);

            // Titre principal
            var title = new Paragraph($"STB - {packType}", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20f
            };
            document.Add(title);

            // Informations client
            var clientInfo = new Paragraph($"Dossier de: {client.Nom} {client.Prenom}\n" +
                                         $"Date de génération: {DateTime.Now:dd/MM/yyyy HH:mm}", infoFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 30f
            };
            document.Add(clientInfo);
        }

        private void AddDocumentSectionTitle(Document document, string title)
        {
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.Blue);
            document.Add(new Paragraph(title, titleFont)
            {
                SpacingAfter = 10f
            });
        }

        private void AddImageToPdf(Document document, string imagePath)
        {
            try
            {
                using (var imageStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    var image = Image.GetInstance(imageStream);

                    // Ajustement de la taille
                    if (image.Width > document.PageSize.Width - 100 ||
                        image.Height > document.PageSize.Height - 100)
                    {
                        image.ScaleToFit(document.PageSize.Width - 100, document.PageSize.Height - 100);
                    }

                    image.Alignment = Image.ALIGN_CENTER;
                    document.Add(image);
                    document.NewPage();
                }
            }
            catch (BadElementException bee)
            {
                _logger.LogError(bee, $"Format d'image non supporté: {imagePath}");
                AddErrorMessage(document, $"Impossible d'afficher l'image: {Path.GetFileName(imagePath)}");
            }
            catch (IOException ioe)
            {
                _logger.LogError(ioe, $"Erreur de lecture du fichier image: {imagePath}");
                AddErrorMessage(document, $"Fichier image corrompu: {Path.GetFileName(imagePath)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur inattendue avec l'image: {imagePath}");
                AddErrorMessage(document, $"Erreur avec l'image: {Path.GetFileName(imagePath)}");
            }
        }

        private void AddUnsupportedTypeMessage(Document document, string fileName)
        {
            document.Add(new Paragraph($"Type de fichier non supporté: {fileName}")
            {
                Font = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10, BaseColor.Blue),
                SpacingAfter = 10f
            });
        }

        private void AddErrorMessage(Document document, string message)
        {
            document.Add(new Paragraph(message)
            {
                Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.Blue),
                SpacingAfter = 10f
            });
        }


        [HttpGet("generate-elyssa-pdf/{demandId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateElyssaPdf(int demandId)
        {
            try
            {
                var demand = await _context.PackElyssa
                    .Include(p => p.Client)
                    .FirstOrDefaultAsync(p => p.Id == demandId);

                if (demand == null)
                {
                    return NotFound("Demande non trouvée");
                }

                var elyssaUploadsPath = Path.Combine(_environment.WebRootPath, "uploads", $"Pack_Elyssa_client_{demand.ClientId}");

                // Vérification de l'existence du répertoire
                if (!Directory.Exists(elyssaUploadsPath))
                {
                    return NotFound("Répertoire des documents introuvable");
                }

                // Construction de la liste des documents avec vérification
                var documents = new List<(string path, string title)>();
                var docDefinitions = new[]
                {
            (demand.PassportPath, "Passeport"),
            (demand.FrenchResidenceProofPath, "Justificatif de résidence en France"),
            (demand.CDIContractPath, "Contrat CDI"),
            (demand.TaxWithholdingCertificatePath, "Certificat de retenue à la source"),
            (demand.LongStayVisaPath, "Visa de long séjour"),
            (demand.VisaRegistrationPath, "Enregistrement du visa")
        };

                foreach (var doc in docDefinitions)
                {
                    if (!string.IsNullOrEmpty(doc.Item1))
                    {
                        var fullPath = Path.Combine(elyssaUploadsPath, doc.Item1);
                        if (System.IO.File.Exists(fullPath))
                        {
                            documents.Add((fullPath, doc.Item2));
                        }
                        else
                        {
                            _logger.LogWarning($"Fichier introuvable: {fullPath}");
                        }
                    }
                }

                if (documents.Count == 0)
                {
                    return BadRequest("Aucun document valide trouvé");
                }

                // Génération du PDF
                var pdfBytes = await GenerateCombinedPdfSafe(documents, demand.Client, "Pack Elyssa");

                // Vérification approfondie du PDF généré
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    throw new Exception("Le PDF généré est vide");
                }

                // Vérification de l'en-tête PDF
                if (pdfBytes.Length < 4 || !pdfBytes.Take(4).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 })) // %PDF
                {
                    _logger.LogError("Le PDF généré n'a pas un en-tête valide");
                    throw new Exception("Le PDF généré est corrompu");
                }

                return File(pdfBytes, "application/pdf", $"PackElyssa_Demande_{demandId}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la génération du PDF. DemandId: {demandId}");
                return StatusCode(500, "Erreur interne du serveur");
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

               

                var notifications = await notificationService.GetClientNotifications(client.Id);
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
                var notification = await _context.NotificationsPack
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.ClientId == client.Id);

                if (notification == null)
                {
                    return NotFound();
                }

                await notificationService.MarkAsRead(notificationId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage de la notification comme lue");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }

        [HttpPost("submit-modification-request")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SubmitModificationRequest([FromForm] ModificationRequestDto requestDto)
        {
            try
            {
                // 1. Vérification de l'utilisateur
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                if (client == null)
                {
                    return NotFound(new { message = "Client non trouvé" });
                }

                // 2. Validation des champs
                var validFields = new[] { "adresse", "profession", "situationProfessionnelle", "etatCivil", "residence" };
                if (!validFields.Contains(requestDto.FieldToModify.ToLower()))
                {
                    return BadRequest(new { message = "Champ à modifier non valide" });
                }

                if (string.IsNullOrEmpty(requestDto.NewValue))
                {
                    return BadRequest(new { message = "La nouvelle valeur est requise" });
                }

                if (requestDto.JustificationFile == null || requestDto.JustificationFile.Length == 0)
                {
                    return BadRequest(new { message = "Un fichier justificatif est requis" });
                }

                // 3. Vérification du type de fichier
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(requestDto.JustificationFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new { message = "Seuls les fichiers PDF, JPG, JPEG et PNG sont autorisés" });
                }

                // 4. Vérification de la taille du fichier (max 5MB)
                if (requestDto.JustificationFile.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(new { message = "La taille du fichier ne doit pas dépasser 5MB" });
                }

                // 5. Création du répertoire de stockage si inexistant
                var uploadsPath = Path.Combine(_environment.WebRootPath, "ModificationRequests");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                // 6. Génération d'un nom de fichier unique
                var fileName = $"Modif_{client.Id}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // 7. Sauvegarde du fichier
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await requestDto.JustificationFile.CopyToAsync(stream);
                }

                // 8. Création de la demande
                var request = new ModificationRequest
                {
                    ClientId = client.Id,
                    FieldToModify = requestDto.FieldToModify,
                    NewValue = requestDto.NewValue,
                    JustificationPath = fileName,
                    RequestDate = DateTime.UtcNow,
                    Status = "EnCours"
                };

                _context.ModificationRequests.Add(request);
                await _context.SaveChangesAsync();

                // 9. Notification (optionnelle)
                //await _notificationService.NotifyNewModificationRequest(request.Id);

                return Ok(new
                {
                    message = "Demande soumise avec succès",
                    requestId = request.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la soumission de la demande");
                return StatusCode(500, new { message = "Erreur interne du serveur" });
            }
        }
        [HttpGet("pending-modification-requests")]
       
        public async Task<IActionResult> GetPendingModificationRequests()
        {
            var requests = await _context.ModificationRequests
                .Include(r => r.Client)
                .Where(r => r.Status == "EnCours")
                .OrderBy(r => r.RequestDate)
                .ToListAsync();

            return Ok(requests);
        }

        [HttpPost("process-modification-request/{requestId}")]
        
        public async Task<IActionResult> ProcessModificationRequest(int requestId, [FromBody] ProcessRequestDto dto)
        {
            var request = await _context.ModificationRequests
                .Include(r => r.Client)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                return NotFound("Demande non trouvée");
            }

            var agentId = GetUserIdFromToken();

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

            request.ProcessedByAgentId = agentId;
            request.ProcessedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notifier le client
            //await _notificationService.NotifyModificationRequestStatus(request.ClientId, request.Id, request.Status);

            return Ok(new { message = $"Demande {request.Status}" });
        }


        [HttpGet("modification-requests/{requestId}/document")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModificationRequestDocument(int requestId)
        {
            try
            {
                var request = await _context.ModificationRequests
                    .Include(r => r.Client)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null || string.IsNullOrEmpty(request.JustificationPath))
                {
                    return NotFound("Document non trouvé");
                }

                var filePath = Path.Combine(_environment.WebRootPath,
                                          "ModificationRequests",
                                          request.JustificationPath);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Fichier introuvable");
                }

                // Déterminer le type MIME correct
                var mimeType = "application/pdf"; // Par défaut
                if (filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    mimeType = "image/jpeg";
                else if (filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    mimeType = "image/png";

                // Nom du fichier pour le téléchargement
                var fileName = $"Justificatif_{request.Client?.Nom}_{request.FieldToModify}{Path.GetExtension(filePath)}";

                return PhysicalFile(filePath, mimeType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la récupération du document {requestId}");
                return StatusCode(500, "Erreur interne du serveur");
            }
        }
        [HttpPut("update")]
        public async Task<IActionResult> UpdateClientInfo([FromBody] UpdateClientDto dto)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                // Mapper le DTO vers l'entité Client
                client.Telephone = dto.Telephone;
                client.Email = dto.Email;
                client.Adresse = dto.Adresse;
                client.Civilite = dto.Civilite;
                client.EtatCivil = dto.EtatCivil;
                client.Residence = dto.Residence;
                client.SituationProfessionnelle = dto.SituationProfessionnelle;
                client.NiveauEducation = dto.NiveauEducation;
                client.NombreEnfants = dto.NombreEnfants;
                client.RevenuMensuel = dto.RevenuMensuel;


                bool isUpdated = await _clientService.UpdateClientInfoAsync(client.Id, client);

                if (!isUpdated)
                {
                    return NotFound(new { message = "Client non trouvé" });
                }

                return Ok(new { message = "Informations mises à jour avec succès !" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise à jour");
                return StatusCode(500, new { message = "Erreur interne" });
            }
        }


    }
}
