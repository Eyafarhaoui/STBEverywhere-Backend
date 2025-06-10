using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STBEverywhere_Back_SharedModels;



using System.IdentityModel.Tokens.Jwt; // Pour JwtRegisteredClaimNames
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_back_APICompte.Repository.IRepository;

using System.Security.Claims;

using STBEverywhere_ApiAuth.Repositories;

using DinkToPdf.Contracts; // Pour les paramètres de conversion

using STBEverywhere_Back_SharedModels.Data;
using QuestPDF.Fluent;

using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.AspNetCore.Hosting;

using STBEverywhere_Back_SharedModels.Models;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.Json;
using MongoDB.Bson.IO;
using Newtonsoft.Json;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using STBEverywhere_Back_SharedModels.Models.enums;
using static System.Runtime.InteropServices.JavaScript.JSType;
using STBEverywhere_back_APICompte.Repository;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.Extensions.Hosting;
using System.Numerics;
using STBEverywhere_back_APICompte.Services.IServices;
using STBEverywhere_back_APICompte.Services;






namespace STBEverywhere_back_APICompte.Controllers
{
    [Route("api/compte")]
    [ApiController]
    public class CompteAPIController : ControllerBase
    {
        private readonly IConverter _pdfConverter;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ICompteService _compteService;
        private readonly ApplicationDbContext _db;
        private readonly IVirementRepository _dbVirement;
        private readonly IUserRepository _userRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CompteAPIController> _logger;
        private readonly IMapper _mapper;
        private readonly HttpClient _httpClient;

        public CompteAPIController(HttpClient httpClient, IConverter pdfConverter, IWebHostEnvironment webHostEnvironment, ICompteService compteService, IUserRepository userRepository, ApplicationDbContext db, IVirementRepository dbVirement, IHttpContextAccessor httpContextAccessor, ILogger<CompteAPIController> logger, IMapper mapper)

        {
            _compteService = compteService;
            _db = db;
            _dbVirement = dbVirement;
            _logger = logger;
            _mapper = mapper;
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;
            _pdfConverter = pdfConverter;
            _webHostEnvironment = webHostEnvironment;

            _httpClient = httpClient;

        }



        [HttpGet("agence-id")]
        public async Task<IActionResult> GetAgenceId(string rib)
        {
            var agenceId = await _compteService.GetAgenceIdOfCompteAsync(rib);
            return Ok(agenceId);
        }
    /*

        [HttpGet("getComptesByAgence/{agenceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetComptesByAgence(string agenceId)
        {
            try
            {
                var comptes = await _compteService.GetComptesByAgenceIdAsync(agenceId);

                if (!comptes.Any())
                {
                    return NotFound(new { message = "Aucun compte trouvé pour cette agence." });
                }

                return Ok(comptes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des comptes par agence");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur serveur" });
            }
        }
        */

        [HttpGet("allComptes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllComptes()
        {
            try
            {
                var comptes = await _compteService.GetAllAsync(); // Sans filtre = tous les comptes

                if (comptes == null || !comptes.Any())
                {
                    return NotFound(new { message = "Aucun compte trouvé dans la base de données." });
                }

                _logger.LogInformation("Liste complète des comptes récupérée avec succès.");
                return Ok(comptes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de tous les comptes.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Erreur serveur lors de la récupération des comptes." });
            }
        }





        [HttpGet("listecompte")]


        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        [ProducesResponseType(StatusCodes.Status500InternalServerError)]

        public async Task<IActionResult> GetComptesByClientId()
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;


            

            var comptes = await _compteService.GetAllAsync(c => c.ClientId == clientId  && c.Type != "Technique");

            if (comptes == null || !comptes.Any())
            {
                return NotFound(new { message = "Aucun compte actif trouvé pour vous." });
            }
            _logger.LogInformation("Getting all comptes");
            return Ok(comptes);
        }
        //liste des comptes qui peuvent effectuer des virements vers d'autres comptes comptes  (tous les comptes sauf compte epargne) 

        [HttpGet("listecompteVirement")]

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetComptesVirementByClientId()
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;

            var comptes = await _compteService.GetAllAsync(c => c.ClientId == clientId && c.Statut != "Clôturé" && c.Statut != "desactive" && c.Type.ToLower() != "epargne" && c.Type != "Technique");


            if (comptes == null || !comptes.Any())
            {
                return NotFound(new { message = "Aucun compte actif trouvé pour vous." });
            }

            _logger.LogInformation("Récupération des comptes actifs non épargne réussie.");
            return Ok(comptes);
        }


        //liste des comptes qui peuvent effectuer des virements vers mes comptes  

        [HttpGet("listecompteVirementMesComptes")]

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetComptesVirementMesComptesByClientId()
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;

            var comptes = await _compteService.GetAllAsync(c => c.ClientId == clientId && c.Statut != "Clôturé" && c.Statut != "desactive"  && c.Type != "Technique");


            if (comptes == null || !comptes.Any())
            {
                return NotFound(new { message = "Aucun compte actif trouvé pour vous." });
            }

            _logger.LogInformation("Récupération des comptes actifs non épargne réussie.");
            return Ok(comptes);
        }

        [HttpPost("CreateCompte")]

        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]


        public async Task<IActionResult> CreateCompte([FromBody] CreateCompteDto compteDto)
        {
            var userId = GetUserIdFromToken();
            var Client = await _userRepository.GetClientByUserIdAsync(userId);
            var agenceid = Client.AgenceId;
            var clientId = Client.Id;
            _logger.LogInformation($"ClientId récupéré depuis le token : {clientId}");


            if (compteDto == null || string.IsNullOrEmpty(compteDto.type))
            {
                _logger.LogError(" Type  obligatoire");
                return BadRequest(new { message = "Type  obligatoires." });
            }

            var clientList = await _compteService.GetAllAsync(c => c.ClientId == clientId);

            _logger.LogInformation($"Nombre de clients trouvés : {clientList.Count()}");

            var client = clientList.FirstOrDefault();
            _logger.LogInformation($"Client trouvé ? {(client != null ? "Oui" : "Non")}");

            if (client == null)
            {
                return BadRequest(new { message = "Aucun client trouvé avec ce NumCin." });
            }

            if (compteDto.type.ToLower() == "courant")
            {

                 var courantCount = (await _compteService.GetAllAsync(c => c.ClientId == clientId && c.Type.ToLower() == "courant")).Count;
                if (courantCount >= 2)
                {
                    return BadRequest(new
                    {
                        message = "Conformément à la politique d’ouverture de comptes sur la plateforme STBEverywhere, un maximum de deux comptes courants est autorisé en ligne. Vous détenez actuellement deux comptes. Pour toute ouverture supplémentaire, nous vous invitons à vous présenter en agence avec les justificatifs requis."
                    });

                }
            }

            if (compteDto.type.ToLower() == "cheque")
            {

                var courantCount = (await _compteService.GetAllAsync(c => c.ClientId == clientId && c.Type.ToLower() == "cheque")).Count;
                if (courantCount >= 2)
                {
                    return BadRequest(new
                    {
                        message = "Conformément à la politique d’ouverture de comptes sur la plateforme STBEverywhere, un maximum de deux comptes chèques est autorisé en ligne. Vous détenez actuellement deux comptes. Pour toute ouverture supplémentaire, nous vous invitons à vous présenter en agence avec les justificatifs requis."
                    });

                }
            }
            if (compteDto.type.ToLower() == "epargne")
            {
                //var epargneCount = (await _dbCompte.GetAllAsync(c => c.NumCin == compteDto.NumCin && c.Type.ToLower() == "epargne")).Count;

                var epargneCount = (await _compteService.GetAllAsync(c => c.ClientId == clientId && c.Type.ToLower() == "epargne")).Count;
                if (epargneCount >= 3)
                {
                   
                    return BadRequest(new
                    {
                        message = "Conformément à la politique d’ouverture de comptes sur la plateforme STBEverywhere, un maximum de trois comptes épargne est autorisé en ligne. Vous détenez actuellement deux comptes. Pour toute ouverture supplémentaire, nous vous invitons à vous présenter en agence avec les justificatifs requis."
                    });

                }
            }
            string generatedRIB = await _compteService.GenerateUniqueRIB(agenceid);
            string iban = _compteService.GenerateIBANFromRIB(generatedRIB);



            // si il s'agit d'un compte epargne solde initial=10 sinon 0
            decimal initialSolde = compteDto.type.ToLower() == "epargne" ? 10 : 0;
            // Utilisation d'AutoMapper pour convertir compteDto en Compte
            var compte = _mapper.Map<Compte>(compteDto);

            // Ajout des valeurs manquantes
            compte.RIB = generatedRIB;
            compte.Solde = initialSolde;
            compte.DateCreation = DateTime.Now;
            compte.Statut = "Actif";
            compte.ClientId = (int)clientId;
            compte.NumCin = client.NumCin;
            compte.NbrOperationsAutoriseesParJour = "illimité";
            compte.MontantMaxAutoriseParJour = "illimité";
            // si il s'agit d'un compte epargne decouvert null sinon 0
            compte.DecouvertAutorise = compteDto.type.ToLower() == "epargne" ? null : 0; 

            compte.IBAN = iban;


            await _compteService.CreateAsync(compte);

           
            return CreatedAtAction(nameof(GetCompteByRIB), new { rib = compte.RIB }, compte);
        }








        [HttpGet("GetByRIB/{rib}")]


        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCompteByRIB(string rib)
        {
            var compte = await _compteService.GetAllAsync(c => c.RIB == rib);
            if (compte == null || !compte.Any())
            {
                return NotFound(new { message = "Aucun compte n'est associé à ce RIB." });
            }

            return Ok(compte);
        }

      
        [HttpPut("Cloturer/{rib}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CloturerCompte(string rib)
        {
            Console.WriteLine("=== Début de la méthode CloturerCompte ===");
            Console.WriteLine($"RIB reçu : {rib}");

            var userId = GetUserIdFromToken();
            Console.WriteLine($"User ID récupéré depuis le token : {userId}");

            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var clientId = client.Id;
            Console.WriteLine($"Client ID récupéré : {clientId}");

            var compte = (await _compteService.GetAllAsync(c => c.RIB == rib)).FirstOrDefault();
            if (compte == null)
            {
                Console.WriteLine("Aucun compte trouvé pour ce RIB.");
                return NotFound(new { message = "Aucun compte bancaire correspondant au RIB fourni n’a été trouvé." });
            }

            Console.WriteLine($"Compte trouvé - Solde : {compte.Solde}");

            // Initialisation des indicateurs
            bool hasCarteActive = false;
            bool hasChequierActif = false;

            // Vérification des cartes actives
            Console.WriteLine("Vérification des cartes associées...");
            var responseCartes = await _httpClient.GetAsync($"http://localhost:5132/api/Carte/rib/{rib}");
            if (responseCartes.IsSuccessStatusCode)
            {
                var json = await responseCartes.Content.ReadAsStringAsync();
                var cartes = JsonConvert.DeserializeObject<List<CarteDTO>>(json);
                hasCarteActive = cartes.Any(c => c.Statut == StatutCarte.Active);
            }
            else
            {
                Console.WriteLine($"Erreur lors de la récupération des cartes (status code: {responseCartes.StatusCode})");
            }

            // Vérification des chéquiers actifs
            Console.WriteLine("Vérification des chèquiers associés...");
            var responseChequiers = await _httpClient.GetAsync($"http://localhost:5264/api/ChequierApi/cheques/{rib}");
            if (responseChequiers.IsSuccessStatusCode)
            {
                var json = await responseChequiers.Content.ReadAsStringAsync();
                var chequiers = JsonConvert.DeserializeObject<List<chequierDTO>>(json);
                hasChequierActif = chequiers.Any(c => c.Status == ChequierStatus.Actif);
            }
            else
            {
                Console.WriteLine($"Erreur lors de la récupération des chèquiers (status code: {responseChequiers.StatusCode})");
            }

            //verification des conditions de refus de cloture
            List<string> motifsRefus = new();

            if (compte.Solde != 0)
            {
                motifsRefus.Add("le solde du compte n’est pas nul");
            }

            if (hasCarteActive)
            {
                motifsRefus.Add("une ou plusieurs cartes bancaires actives sont associées à ce compte");
            }

            if (hasChequierActif)
            {
                motifsRefus.Add("un ou plusieurs chèquiers actifs sont liés à ce compte");
            }

            if (motifsRefus.Count > 0)
            {
                var messageFinal = "La demande de clôture du compte n’a pas pu aboutir pour les raisons suivantes : " +
                                   string.Join(", ", motifsRefus) + ". " +
                                   "Merci de régulariser la situation avant de renouveler votre demande.";

                Console.WriteLine("Clôture refusée : " + messageFinal);
                return BadRequest(new { message = messageFinal });
            }

            // Clôture du compte
            compte.Statut = "Clôturé";
            await _compteService.SaveAsync();

            Console.WriteLine("Compte clôturé avec succès.");
            Console.WriteLine("=== Fin de la méthode CloturerCompte ===");

            return Ok(new { message = "Votre compte a été clôturé avec succès. Merci pour votre confiance." });
        }




        

        [HttpGet("GetSoldeByRIB/{rib}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        public async Task<IActionResult> GetSoldeByRIB(string rib)
        {
            try
            {
                var solde = await _compteService.GetSoldeByRIBAsync(rib);
                return Ok(new { Solde = solde });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("CreateCompteTechnique")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCompteTechnique([FromBody] CreateCompteDto compteDto)
        {
            var userId = GetUserIdFromToken();
            var client = await _userRepository.GetClientByUserIdAsync(userId);
            var agenceid = client.AgenceId;
            try
            {
                _logger.LogInformation("Début de création d'un compte technique");





                // Génération des identifiants
                string generatedRIB = await _compteService.GenerateUniqueRIB(agenceid);
                string iban = _compteService.GenerateIBANFromRIB(generatedRIB);


                var compte = new Compte
                {
                    RIB = generatedRIB,
                    IBAN = iban,
                    Type = "Technique",
                    Solde = 0,
                    DateCreation = DateTime.Now,
                    Statut = "Actif",
                    ClientId = client.Id,
                    NumCin = "TECHNIQUE", 
                    NbrOperationsAutoriseesParJour = "illimité",
                    MontantMaxAutoriseParJour = "illimité",
                    DecouvertAutorise = null 
                };

                await _compteService.CreateAsync(compte);
                _logger.LogInformation($"Compte technique créé avec RIB: {compte.RIB}");

                return CreatedAtAction(nameof(GetCompteByRIB), new { rib = compte.RIB }, new
                {
                    RIB = compte.RIB,
                    IBAN = compte.IBAN,
                    Type = compte.Type,

                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création du compte technique");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Une erreur interne est survenue lors de la création du compte technique."
                });
            }
        }



        private async Task<byte[]> GeneratePdfWithQuestPDF(Client client, string rib, IWebHostEnvironment hostingEnvironment, string iban, DateTime dateCreation)
        {
            try
            {
                var apiAgenceUrl = $"http://localhost:5036/api/AgenceApi/byId/{client.AgenceId}";

                var response = await _httpClient.GetAsync(apiAgenceUrl);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Erreur HTTP: {response.StatusCode} - {content}");
                }

                var agence = JsonConvert.DeserializeObject<Agence>(content);
                //construction du chemin du logo
                string logoPath = Path.Combine(hostingEnvironment.WebRootPath, "images", "STBlogo.jpg");

                TextStyle arabicTextStyle = TextStyle.Default
                    .FontFamily("Arial")
                    .FontSize(8)
                    .DirectionFromRightToLeft();

                return Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(12));

                        // En-tête
                        page.Header()
                            .Column(headerCol =>
                            {
                                //Logo + Texte
                                headerCol.Item().PaddingBottom(10).Row(row =>
                                {
                                    row.ConstantItem(100).Column(logoCol =>
                                    {
                                        logoCol.Item().Height(40).Image(logoPath, ImageScaling.FitArea);
                                        logoCol.Item().Container().AlignRight().Text("STB BANK").Bold().FontSize(12);
                                    });
                                    row.RelativeItem();
                                });

                                // Texte bilingue
                                headerCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(t =>
                                    {
                                        t.Span("Ce relevé est destiné à être remis à vos créanciers ou débiteurs").FontSize(8);
                                        t.EmptyLine();
                                        t.Span("nationaux ou internationaux (Virements, prélèvements, etc.)").FontSize(8);
                                        t.Span("nationaux ou internationaux (Virements, prélèvements, etc.)").FontSize(8);
                                        t.EmptyLine();
                                        t.Span("Garantit le bon enregistrement des opérations bancaires").FontSize(8);
                                    });

                                    row.RelativeItem().Container().AlignRight().Text(t =>
                                    {
                                        t.Span("يُعدّ هذا الكشف مرجعاً مصرفياً رسمياً يُقدَّم إلى الجهات الدائنة أو المدينة، على المستويين الوطني والدولي").Style(arabicTextStyle);
                                        t.EmptyLine();
                                        t.Span("يُستعمل لإجراء التحويلات البنكية والاقتطاعات وغيرها من العمليات المالية").Style(arabicTextStyle);
                                        t.EmptyLine();
                                        t.Span("يُعتمد هذا المستند لضمان دقة تسجيل المعاملات المرتبطة بالحساب البنكي المعني").Style(arabicTextStyle);
                                    });
                                });

                                headerCol.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Black);
                                headerCol.Item().PaddingTop(5).AlignCenter().Text("RELEVÉ D'IDENTITÉ BANCAIRE RIB").Bold().FontSize(16);
                            });

                        // Contenu principal
                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(col =>
                            {
                                // Informations client
                                col.Item().Text("Titulaire du compte :");
                                col.Item().Text($"{client.Nom} {client.Prenom}");
                                col.Item().Text(client.Adresse);
                                col.Item().AlignRight().Text($"Date de création : {dateCreation:dd/MM/yyyy}");

                                col.Item().Height(20);

                                // Tableau RIB, IBAN et BIC
                                col.Item().AlignCenter().Column(centerCol =>
                                {
                                    // Tableau RIB
                                    if (!string.IsNullOrEmpty(rib))
                                    {
                                        var ribCleaned = rib.Replace(" ", "");
                                        var codeBanque = ribCleaned.Length >= 2 ? ribCleaned.Substring(0, 2) : "";
                                        var codeAgence = ribCleaned.Length >= 5 ? ribCleaned.Substring(2, 3) : "";
                                        var numCompte = ribCleaned.Length >= 15 ? ribCleaned.Substring(5, 10) : "";
                                        var nat = ribCleaned.Length >= 18 ? ribCleaned.Substring(15, 3) : "";
                                        var cleRib = ribCleaned.Length >= 20 ? ribCleaned.Substring(18, 2) : "";

                                        centerCol.Item().Table(table =>
                                        {
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.ConstantColumn(50);
                                                columns.ConstantColumn(50);
                                                columns.ConstantColumn(100);
                                                columns.ConstantColumn(50);
                                                columns.ConstantColumn(50);
                                            });

                                            table.Header(header =>
                                            {
                                                header.Cell().Border(1).AlignCenter().Text("Code Banque").Bold();
                                                header.Cell().Border(1).AlignCenter().Text("Code Agence").Bold();
                                                header.Cell().Border(1).AlignCenter().Text("Numéro de compte").Bold();
                                                header.Cell().Border(1).AlignCenter().Text("Nat").Bold();
                                                header.Cell().Border(1).AlignCenter().Text("Clé RIB").Bold();
                                            });

                                            table.Cell().Border(1).AlignCenter().Text(codeBanque);
                                            table.Cell().Border(1).AlignCenter().Text(codeAgence);
                                            table.Cell().Border(1).AlignCenter().Text(numCompte.Insert(6, "."));
                                            table.Cell().Border(1).AlignCenter().Text(nat);
                                            table.Cell().Border(1).AlignCenter().Text(cleRib);
                                        });
                                    }

                                    // IBAN 
                                    if (!string.IsNullOrEmpty(iban))
                                    {
                                        var cleanedIban = iban.Replace(" ", "");
                                        var formattedIban = string.Empty;

                                        if (cleanedIban.Length >= 2)
                                        {
                                            formattedIban = cleanedIban.Substring(0, 2);
                                            if (cleanedIban.Length > 2)
                                            {
                                                formattedIban += " " + cleanedIban.Substring(2, 2);
                                                for (int i = 4; i < cleanedIban.Length; i += 4)
                                                {
                                                    int length = Math.Min(4, cleanedIban.Length - i);
                                                    formattedIban += " " + cleanedIban.Substring(i, length);
                                                }
                                            }
                                        }

                                        centerCol.Item().PaddingTop(15).Text("IBAN International Bank Account Number").FontSize(10).Bold();
                                        centerCol.Item().Text(formattedIban).FontSize(10);
                                    }

                                    // BIC
                                    centerCol.Item().PaddingTop(10).Text("BIC Bank Identifier Code").FontSize(10).Bold();
                                    centerCol.Item().Text("STBKTNTT").FontSize(10);
                                });

                                // Pied de page
                                col.Item().PaddingTop(20).Column(agenceCol =>
                                {
                                    
                                    //agenceCol.Item().Text($"Agence : {agence.}");
                                    agenceCol.Item().Text($"Adresse : {agence.Libelle}");
                                    agenceCol.Item().Text($"{agence.DR}");
                                    agenceCol.Item().Text($"Téléphone : {agence.tel1}");
                                    agenceCol.Item().Text($"Fax :{agence.fax}");
                                });

                                col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Black);
                                col.Item().AlignRight().Text($"Tunis le, {DateTime.Now:dd/MM/yyyy}");
                            });
                    });
                }).GeneratePdf();
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de l'appel HTTP: {ex.Message}");
            }

        }



        [HttpGet("rib/download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadRIB(string rib)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                if (client == null)
                    return NotFound(new { message = "Client non trouvé" });

                var comptes = await _compteService.GetAllAsync(c => c.ClientId == client.Id && c.Statut != "Clôturé" && c.Type != "Technique");

                if (comptes == null || !comptes.Any())
                    return NotFound(new { message = "Aucun compte actif trouvé pour ce client" });

                var compte = comptes.FirstOrDefault();
                if (string.IsNullOrEmpty(compte?.RIB))
                    return NotFound(new { message = "RIB non disponible pour ce compte" });

                QuestPDF.Settings.License = LicenseType.Community;


                var pdfBytes = await _compteService.GeneratePdfRIBWithQuestPDF(client, rib, _webHostEnvironment, compte.IBAN, compte.DateCreation);
                return File(pdfBytes, "application/pdf", $"RIB_{client.Nom}.pdf");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération du RIB");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Une erreur est survenue lors de la génération du document" });
            }
        }







        [HttpPut("Debiter/{rib}/{montant}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DebiterCompte(string rib, decimal montant)
        {
            var compte = (await _compteService.GetAllAsync(c => c.RIB == rib)).FirstOrDefault();

            if (compte == null)
                return NotFound("Compte introuvable.");

            if (compte.Solde + compte.DecouvertAutorise < montant)
                return BadRequest("Solde insuffisant.");

            compte.Solde -= montant;
            await _compteService.UpdateAsync(compte);

            return Ok(new { message = "Compte débité avec succès." });
        }


        [HttpGet("extrait/download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadExtrait(string rib, [FromQuery] DateTime datedebut, [FromQuery] DateTime dateFin)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var client = await _userRepository.GetClientByUserIdAsync(userId);

                if (client == null) return NotFound(new { message = "Client non trouvé" });

                var comptes = await _compteService.GetAllAsync(c => c.Type != "Technique"&& c.RIB==rib);

                if (comptes == null || !comptes.Any())
                    return NotFound(new { message = "Aucun compte trouvé pour ce rib" });

                var compte = comptes.FirstOrDefault();
                

                var debut = datedebut.Date; 
                var fin = dateFin.Date.AddDays(1).AddTicks(-1); 
                var pdfBytes = await _compteService.GeneratePdfExtraitWithQuestPDF(rib, debut, fin, compte.Statut,compte.IBAN,compte.Solde, _webHostEnvironment);
                return File(pdfBytes, "application/pdf", $"RIB_{client.Nom}.pdf");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération du RIB");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Une erreur est survenue lors de la génération du document" });
            }
        }



















        private int GetUserIdFromToken()
        {
            try
            {
                //recupere l’en-tête HTTP Authorization qui contient Bearer <token>
                var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader))
                {
                    throw new UnauthorizedAccessException("Header Authorization manquant");
                }

                var tokenParts = authHeader.Split(' ');
                //vérifie si format de l’en-tête  Authorization est Bearer <token>
                if (tokenParts.Length != 2 || !tokenParts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Format d'autorisation invalide");
                }
                //extrait token
                var token = tokenParts[1].Trim();
                var handler = new JwtSecurityTokenHandler();
                //verifie si token est jwt
                if (!handler.CanReadToken(token))
                {
                    throw new UnauthorizedAccessException("Le token n'est pas un JWT valide");
                }
                //lecture de jwt pour pouvoir acceder a ces claims
                var jwtToken = handler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                // id peut avoir comme claim le nom sub ou NameIdentifier
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

       


