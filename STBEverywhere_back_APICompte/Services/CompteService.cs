using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.Fonts;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APICompte.Repository;
using STBEverywhere_back_APICompte.Repository.IRepository;
using STBEverywhere_back_APICompte.Services.IServices;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels.Models.DTO;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.Json;
namespace STBEverywhere_back_APICompte.Services
{
    public class CompteService : ICompteService
    {

        private readonly ICompteRepository _compteRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CompteService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly HttpClient _httpClient;
        private readonly IVirementRepository _dbVirement;
        private readonly ApplicationDbContext _db;
        public CompteService(ApplicationDbContext db, IVirementRepository dbVirement,HttpClient httpClient, ICompteRepository compteRepository,IUserRepository userRepository,IHttpClientFactory httpClientFactory, ILogger<CompteService> logger)
        {
            _compteRepository = compteRepository;
            _userRepository = userRepository;
            _httpClientFactory = httpClientFactory; 
            _logger = logger;
            _httpClient = httpClient;
            _dbVirement = dbVirement;
            _db = db;
        }

        public async Task<IEnumerable<DemandeModificationDecouvert>> GetDemandesModificationAsync(string ribCompte, StatutDemandeEnum statut)
        {
            return await _compteRepository.GetDemandesModificationAsync(ribCompte, statut.ToString());
        }
       

        public async Task CreateDemandeModificationAsync(DemandeModificationDecouvert demande)
        {
            await _compteRepository.CreateDemandeModificationAsync(demande);
        }


      



        public async Task<Compte> GetByRIBAsync(string rib)
        {
            return await _compteRepository.GetByRibAsync(rib);
        }


        public async Task<string> GetAgenceIdOfCompteAsync(string rib)
        {
            var Compte = await _compteRepository.GetCompteByRIBAsync(rib);

           
           
                var client = await _userRepository.GetClientByUserIdAsync(Compte.ClientId);


            return client.AgenceId ;
        }




        public async Task<IEnumerable<DemandeModificationDecouvert>> GetDemandesByAgenceIdAsync(string agenceId)
        {
            var allComptes = await _compteRepository.GetAllAsync();

            var comptesAvecAgence = new List<Compte>();

            foreach (var compte in allComptes)
            {
                var client = await _userRepository.GetClientByUserIdAsync(compte.ClientId);
                if (client != null && client.AgenceId == agenceId)
                {
                    comptesAvecAgence.Add(compte);
                }
            }

            var demandes = new List<DemandeModificationDecouvert>();

            foreach (var compte in comptesAvecAgence)
            {
                var demandesPourCompte = await _compteRepository.GetDemandesModificationByCompteRibAsync(compte.RIB);
                demandes.AddRange(demandesPourCompte);
            }

            return demandes;
        }




        public async Task<List<Compte>> GetAllAsync(Expression<Func<Compte, bool>> filter = null)
        {
            return await _compteRepository.GetAllAsync(filter);
        }

        public async Task<Compte> GetByRibAsync(string rib)
        {
            return await _compteRepository.GetByRibAsync(rib);
        }

        public async Task<Compte> UpdateAsync(Compte entity)
        {
            return await _compteRepository.UpdateAsync(entity);
        }

        public async Task<Compte> CreateAsync(Compte entity)
        {
            await _compteRepository.CreateAsync(entity);
            return entity;
        }
        public async Task<Client> GetClientByRIBAsync(string rib)
        {
            var compte = await _compteRepository.GetCompteByRIBAsync(rib);
            if (compte == null)
                return null;

            return compte.Client; // Retourne le client associé au compte
        }
        public async Task<decimal> GetSoldeByRIBAsync(string rib)
        {
            var compte = await _compteRepository.GetByRibAsync(rib);
            if (compte == null)
            {
                throw new InvalidOperationException("Compte introuvable.");
            }

            return compte.Solde;
        }

        public async Task SaveAsync()
        {
            await _compteRepository.SaveAsync();
        }

        public string GenerateIBANFromRIB(string rib)
        {
            if (string.IsNullOrWhiteSpace(rib) || rib.Length < 20)
            {
                throw new ArgumentException("Le RIB doit contenir au moins 20 caractères numériques.");
            }

            Random random = new Random();
            string randomDigits = random.Next(10, 99).ToString(); // Génère deux chiffres aléatoires
            // Prend 10 caractères à partir de la position 3 Prend 2 caractères à partir de la position 18
            string iban = $"TN{randomDigits}10{rib.Substring(2, 3)}{rib.Substring(5, 10)}{rib.Substring(18, 2)}";

            return iban;
        }

        public async Task<IEnumerable<DemandeModificationDecouvert>> GetDemandesByClientIdAsync(int clientId)
        {
            // 1. Récupérer tous les comptes du client
            var comptes = await _compteRepository.GetAllAsync(c => c.ClientId == clientId);

            // 2. Extraire les RIBs de ces comptes
            var ribComptes = comptes.Select(c => c.RIB).ToList();

            // 3. Récupérer toutes les demandes pour ces RIBs
            return await _compteRepository.GetDemandesModificationAsync(ribComptes);
        }



      




        public async Task<string> GenerateUniqueRIB(string agenceid)
        {
            var agenceCode = await GetAgenceCodeAsync(agenceid);

            if (string.IsNullOrEmpty(agenceCode) || agenceCode.Length != 3 || !agenceCode.All(char.IsDigit))
            {
                throw new InvalidOperationException("Code agence invalide.");
            }

            string rib;
            var random = new Random();

            do
            {
                var ribBuilder = new StringBuilder("10"); // les deux premiers chiffres

                ribBuilder.Append(agenceCode); // les 3 suivants = code agence

                // Le reste du rib est géneré aleatoirement
                for (int i = 0; i < 15; i++)
                {
                    ribBuilder.Append(random.Next(0, 10)); // chiffre entre 0 et 9
                }

                rib = ribBuilder.ToString();
            }
            // Si le RIB existe déjà on recommence le do pour generer nv RIB sinon on sort de la boucle
            while (await _compteRepository.ExistsByRibAsync(rib));

            return rib;
        }

        private async Task<string> GetAgenceCodeAsync(string agenceid)
        {
            try
            {




                var httpClient = _httpClientFactory.CreateClient("AgenceService");
                var response = await httpClient.GetAsync($"/api/AgenceApi/byId/{agenceid}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Erreur récupération agence: {Code}", response.StatusCode);
                    throw new InvalidOperationException("Échec de la récupération de l'agence.");
                }

                var agence = await response.Content.ReadFromJsonAsync<AgenceDto>();
                //si codeagence nul on retourne null sinon on retourne codeagence 
                return agence?.CodeAgence; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération du code agence.");
                throw;
            }
        }

       






        public async Task<byte[]> GeneratePdfRIBWithQuestPDF(Client client, string rib, IWebHostEnvironment hostingEnvironment, string iban, DateTime dateCreation)
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
                                // Section Logo + Texte
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

                                // Section centrée - Tableau RIB, IBAN et BIC
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

                                    // formater iban pour affciher espacs apres 4 caracteres apres le code pays et les 2 chiffres aleatoire 
                                    if (!string.IsNullOrEmpty(iban))
                                    {
                                        //ssupprimer espace
                                        var cleanedIban = iban.Replace(" ", "");
                                        var formattedIban = string.Empty;

                                        if (cleanedIban.Length >= 2)
                                        {
                                            //extraire code pays
                                            
                                            formattedIban = cleanedIban.Substring(0, 2);
                                            if (cleanedIban.Length > 2)
                                            {
                                                // code pays+espace+ 2 chiffres aleatoire 
                                                formattedIban += " " + cleanedIban.Substring(2, 2);
                                                // a partir du 5eme caractere on fait espace chaque 4 caracter
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


        public async Task<byte[]> GeneratePdfExtraitWithQuestPDF(string rib, DateTime dateDebut, DateTime dateFin, string Statut, string IBAN, decimal solde, IWebHostEnvironment hostingEnvironment)
        {
            var lignesExtrait = new List<dynamic>();

            var soldeInitial = await _db.HistoriquesSoldes
                .Where(h => h.RIB == rib && h.date_jour.Date == dateDebut.Date)
                .Select(h => h.Solde)
                .FirstOrDefaultAsync();

            var virements = await _dbVirement.GetAllAsync(v =>
                (v.RIB_Emetteur == rib || v.RIB_Recepteur == rib) &&
                v.DateVirement >= dateDebut && v.DateVirement <= dateFin);

            var fraisComptes = await _db.FraisComptes
                .Where(f => f.RIB == rib && f.Date >= dateDebut && f.Date <= dateFin)
                .ToListAsync();
            var idsFraisDéjàAjoutés = new HashSet<int>(); // pour éviter les doublons

            var listeOperationsAvecFrais = new List<dynamic>();
            foreach (var v in virements.OrderBy(v => v.DateVirement))
            {
                listeOperationsAvecFrais.Add(new
                {
                    Date = v.DateVirement.ToString("dd/MM/yyyy"),
                    Libelle = v.Motif,
                    Debit = v.RIB_Emetteur == rib ? v.Montant.ToString("F3") : "",
                    Credit = v.RIB_Recepteur == rib ? v.Montant.ToString("F3") : ""
                });

                var fraisAssocies = fraisComptes
    .Where(f => f.IdsVirementsStr != null &&
                f.IdsVirementsStr.Split(',').Contains(v.Id.ToString()) &&
                f.RIB == rib &&
                !idsFraisDéjàAjoutés.Contains(f.Id)) // éviter doublon
    .OrderBy(f => f.Date);

                foreach (var f in fraisAssocies)
                {
                    listeOperationsAvecFrais.Add(new
                    {
                        Date = f.Date.ToString("dd/MM/yyyy"),
                        Libelle = f.type,
                        Debit = f.Montant.ToString("F3"),
                        Credit = ""
                    });

                    idsFraisDéjàAjoutés.Add(f.Id); // marquer comme ajouté
                }

            }

            var idsVirementsUtilisés = virements.Select(v => v.Id.ToString()).ToHashSet();
            var fraisIsolés = fraisComptes
                .Where(f => string.IsNullOrEmpty(f.IdsVirementsStr) || !f.IdsVirementsStr.Split(',').Any(id => idsVirementsUtilisés.Contains(id)))
                .OrderBy(f => f.Date)
                .Select(f => new
                {
                    Date = f.Date.ToString("dd/MM/yyyy"),
                    Libelle = f.type,
                    Debit = f.Montant.ToString("F3"),
                    Credit = ""
                });

            listeOperationsAvecFrais.AddRange(fraisIsolés);

            var fraisChequiersResponse = await _httpClient.GetAsync($"http://localhost:5264/api/ChequierApi/frais/by-rib/{rib}");
            if (fraisChequiersResponse.IsSuccessStatusCode)
            {
                var json = await fraisChequiersResponse.Content.ReadAsStringAsync();
                var fraisChequiers = System.Text.Json.JsonSerializer.Deserialize<List<FraisChequierDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                listeOperationsAvecFrais.AddRange(fraisChequiers
                    .Where(f => f.Date >= dateDebut && f.Date <= dateFin)
                    .Select(f => new
                    {
                        Date = f.Date.ToString("dd/MM/yyyy"),
                        Libelle = f.Type,
                        Debit = f.Montant.ToString("F3"),
                        Credit = ""
                    }));
            }

            var fraisCartesResponse = await _httpClient.GetAsync($"http://localhost:5132/api/Carte/frais-cartes/by-rib/{rib}");
            if (fraisCartesResponse.IsSuccessStatusCode)
            {
                var json = await fraisCartesResponse.Content.ReadAsStringAsync();
                var fraisCartes = System.Text.Json.JsonSerializer.Deserialize<List<FraisCarteDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                listeOperationsAvecFrais.AddRange(fraisCartes
                    .Where(f => f.Date >= dateDebut && f.Date <= dateFin)
                    .Select(f => new
                    {
                        Date = f.Date.ToString("dd/MM/yyyy"),
                        Libelle = $"Frais carte - {f.Type}",
                        Debit = f.Montant.ToString("F3"),
                        Credit = ""
                    }));
            }

            listeOperationsAvecFrais = listeOperationsAvecFrais.OrderBy(o => DateTime.ParseExact(o.Date, "dd/MM/yyyy", null)).ToList();

            var totalDebit = listeOperationsAvecFrais.Sum(l => decimal.TryParse((string)l.Debit, out var d) ? d : 0);
            var totalCredit = listeOperationsAvecFrais.Sum(l => decimal.TryParse((string)l.Credit, out var c) ? c : 0);

            /* var soldeFinal = await _db.HistoriquesSoldes
                 .Where(h => h.RIB == rib && h.date_jour.Date == dateFin.Date)
                 .Select(h => h.Solde)
                 .FirstOrDefaultAsync();*/
            var soldeFinal = (soldeInitial + totalCredit) - totalDebit;
             listeOperationsAvecFrais.Add(new
            {
                Date = "",
                Libelle = "Total des opérations",
                Debit = totalDebit.ToString("F3"),
                Credit = totalCredit.ToString("F3")
            });

            string numeroCompte = rib.Substring(5, 13);
            string logoPath = Path.Combine(hostingEnvironment.WebRootPath, "images", "STBlogo.jpg");
            string dateActuelle = DateTime.Now.ToString("dd/MM/yyyy");

            var defaultTextStyle = TextStyle.Default.FontFamily("Arial").FontSize(12);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.DefaultTextStyle(defaultTextStyle);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);

                    page.Header().ShowOnce().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(colGauche =>
                            {
                                colGauche.Item().Text("SOCIÉTÉ TUNISIENNE DE LA BANQUE").Bold().FontSize(12);
                                colGauche.Item().Text("Société Anonyme au capital de 251 000,000 DT").FontSize(10);
                            });

                            row.ConstantItem(90).AlignCenter().Image(logoPath, ImageScaling.FitArea);

                            row.RelativeItem().AlignRight().Column(colDroite =>
                            {
                                colDroite.Item().Text("الشركة التونسية للبنك").FontSize(12);
                                //colDroite.Item().Text("شركة خفية الإسم، رأس مالها 251.000.000").FontSize(10);

                                colDroite.Item().Text("شركة خفية الإسم، رأس مالها 000,000 152  د.ت").FontSize(10);



                            });
                        });

                        col.Item().PaddingTop(15);
                        col.Item().AlignRight().Text($"Tunis le : {dateActuelle}").FontSize(10);
                        col.Item().AlignCenter().Text("EXTRAIT DU COMPTE").FontSize(16).Bold().Italic();
                        col.Item().AlignCenter().Text($"Période : {dateDebut:dd/MM/yyyy} - {dateFin:dd/MM/yyyy}").FontSize(10);

                        col.Item().PaddingTop(10);
                        col.Item().AlignLeft().Text($"Numéro de compte : {numeroCompte}");
                        col.Item().AlignLeft().Text($"Devise : TND");
                        col.Item().AlignLeft().Text($"IBAN : {IBAN}");
                        col.Item().AlignLeft().Text($"État du compte : {Statut}");
                        col.Item().AlignRight().PaddingTop(5).Text($"Solde initial au {dateDebut:dd/MM/yyyy} : {soldeInitial:F3} TND").Bold();
                    });

                    page.Content().PaddingVertical(20).Column(content =>
                    {
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2); // Date
                                columns.RelativeColumn(4); // Libellé
                                columns.RelativeColumn(2); // Débit
                                columns.RelativeColumn(2); // Crédit
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyleCenter).Text("Date");
                                header.Cell().Element(CellStyleCenter).Text("Libellé");
                                header.Cell().Element(CellStyleCenter).Text("Débit");
                                header.Cell().Element(CellStyleCenter).Text("Crédit");

                                static IContainer CellStyleCenter(IContainer container) =>
                                    container
                                        .DefaultTextStyle(x => x.Bold())
                                        .Background("#E0E0E0")
                                        .Border(1)
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .Padding(5);
                            });

                            foreach (var ligne in listeOperationsAvecFrais)
                            {
                                table.Cell().Element(CellDataStyle).Text((string)ligne.Date);
                                table.Cell().Element(CellDataStyle).Text((string)ligne.Libelle);
                                table.Cell().Element(CellDataStyle).AlignRight().Text((string)ligne.Debit);
                                table.Cell().Element(CellDataStyle).AlignRight().Text((string)ligne.Credit);
                            }

                            static IContainer CellDataStyle(IContainer container) =>
                                container.Border(1).Padding(5);
                        });

                        content.Item().AlignRight().PaddingTop(10).Text($"Solde final au {dateFin:dd/MM/yyyy} : {soldeFinal:F3} TND").Bold();
                    });
                });
            });

            return document.GeneratePdf();
        }







        /*
         public async Task<byte[]> GeneratePdfExtraitWithQuestPDF(string rib, DateTime dateDebut, DateTime dateFin, string Statut, string IBAN, decimal solde, IWebHostEnvironment hostingEnvironment)
         {
             var lignesExtrait = new List<dynamic>();

             var virements = await _dbVirement.GetAllAsync(v =>
                 (v.RIB_Emetteur == rib || v.RIB_Recepteur == rib) &&
                 v.DateVirement >= dateDebut && v.DateVirement <= dateFin);

             lignesExtrait.AddRange(virements.Select(v => new
             {
                 Date = v.DateVirement.ToString("dd/MM/yyyy"),
                 Libelle = v.TypeVirement,
                 Debit = v.RIB_Emetteur == rib ? v.Montant.ToString("F3") : "",
                 Credit = v.RIB_Recepteur == rib ? v.Montant.ToString("F3") : ""
             }));

             var fraisComptes = await _db.FraisComptes
                 .Where(f => f.RIB == rib && f.Date >= dateDebut && f.Date <= dateFin)
                 .ToListAsync();

             lignesExtrait.AddRange(fraisComptes.Select(f => new
             {
                 Date = f.Date.ToString("dd/MM/yyyy"),
                 Libelle = f.type,
                 Debit = "",
                 Credit = f.Montant.ToString("F3")
             }));

             var fraisChequiersResponse = await _httpClient.GetAsync($"http://localhost:5264/api/ChequierApi/frais/by-rib/{rib}");
             if (fraisChequiersResponse.IsSuccessStatusCode)
             {
                 var jsonChequiers = await fraisChequiersResponse.Content.ReadAsStringAsync();
                 var fraisChequiers = System.Text.Json.JsonSerializer.Deserialize<List<FraisChequierDto>>(jsonChequiers, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                 lignesExtrait.AddRange(fraisChequiers
                     .Where(f => f.Date >= dateDebut && f.Date <= dateFin)
                     .Select(f => new
                     {
                         Date = f.Date.ToString("dd/MM/yyyy"),
                         Libelle = f.Type,
                         Debit = "",
                         Credit = f.Montant.ToString("F3")
                     }));
             }

             var fraisCartesResponse = await _httpClient.GetAsync($"http://localhost:5132/api/Carte/frais-cartes/by-rib/{rib}");
             if (fraisCartesResponse.IsSuccessStatusCode)
             {
                 var jsonCartes = await fraisCartesResponse.Content.ReadAsStringAsync();
                 var fraisCartes = System.Text.Json.JsonSerializer.Deserialize<List<FraisCarteDto>>(jsonCartes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                 lignesExtrait.AddRange(fraisCartes
                     .Where(f => f.Date >= dateDebut && f.Date <= dateFin)
                     .Select(f => new
                     {
                         Date = f.Date.ToString("dd/MM/yyyy"),
                         Libelle = $"Frais carte - {f.Type}",
                         Debit = "",
                         Credit = f.Montant.ToString("F3")
                     }));
             }

             lignesExtrait = lignesExtrait.OrderBy(l => DateTime.ParseExact(l.Date, "dd/MM/yyyy", null)).ToList();

             var totalDebit = lignesExtrait.Cast<dynamic>().Sum(l => decimal.TryParse((string)l.Debit, out decimal d) ? d : 0);
             var totalCredit = lignesExtrait.Cast<dynamic>().Sum(l => decimal.TryParse((string)l.Credit, out decimal c) ? c : 0);

             lignesExtrait.Add(new
             {
                 Date = "",
                 Libelle = "Total des opérations",
                 Debit = totalDebit.ToString("F3"),
                 Credit = totalCredit.ToString("F3")
             });

             string numeroCompte = rib.Substring(5, 13);
             string logoPath = Path.Combine(hostingEnvironment.WebRootPath, "images", "STBlogo.jpg");
             string dateActuelle = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

             var defaultTextStyle = TextStyle.Default.FontFamily("Arial").Fallback(TextStyle.Default.FontFamily("Times New Roman")).FontSize(12);

             var document = Document.Create(container =>
             {
                 container.Page(page =>
                 {
                     page.DefaultTextStyle(defaultTextStyle);
                     page.Margin(30);

                     page.Header().Element(header =>
                     {
                         header.Column(col =>
                         {
                             col.Item().Row(row =>
                             {
                                 row.RelativeItem().Column(colGauche =>
                                 {
                                     colGauche.Item().Text("SOCIÉTÉ TUNISIENNE DE LA BANQUE").Bold().FontSize(12);
                                     colGauche.Item().Text("Société Anonyme au capital de 251.000.000 DT").FontSize(10);
                                 });

                                 row.ConstantItem(90).AlignCenter().Image(logoPath, ImageScaling.FitArea);

                                 row.RelativeItem().AlignRight().Column(colDroite =>
                                 {
                                     colDroite.Item().Text("الشركة التونسية للبنك").FontSize(12);
                                     colDroite.Item().Text("شركة خفية الإسم، رأس مالها 251.000.000").FontSize(10);
                                 });
                             });

                             col.Item().PaddingTop(15);
                             col.Item().AlignRight().Text($"Tunis le : {dateActuelle}").FontSize(10);
                             col.Item().AlignCenter().Text("EXTRAIT DU COMPTE").FontSize(16).Bold().Italic();
                             col.Item().AlignCenter().Text($"Période : {dateDebut:dd/MM/yyyy} - {dateFin:dd/MM/yyyy}").FontSize(10);

                             col.Item().PaddingTop(10);
                             col.Item().AlignLeft().Text($"Numéro de compte : {numeroCompte}");
                             col.Item().AlignLeft().Text($"Devise : TND");
                             col.Item().AlignLeft().Text($"IBAN : {IBAN}");
                             col.Item().AlignLeft().Text($"État du compte : {Statut}");
                         });
                     });

                     page.Content().PaddingTop(20).Column(content =>
                     {
                         content.Item().Table(table =>
                         {
                             table.ColumnsDefinition(columns =>
                             {
                                 columns.RelativeColumn(2);
                                 columns.RelativeColumn(3);
                                 columns.RelativeColumn(2);
                                 columns.RelativeColumn(2);
                             });

                             table.Header(header =>
                             {
                                 header.Cell().Element(CellStyle).Text("Date");
                                 header.Cell().Element(CellStyle).Text("Libellé");
                                 header.Cell().Element(CellStyle).Text("Débit");
                                 header.Cell().Element(CellStyle).Text("Crédit");

                                 static IContainer CellStyle(IContainer container) =>
                                     container.DefaultTextStyle(x => x.Bold()).Padding(5).Background("#EEE");
                             });

                             foreach (var ligne in lignesExtrait.Cast<dynamic>())
                             {
                                 table.Cell().Element(CellDataStyle).Element(c => c.Text((string)ligne.Date));
                                 table.Cell().Element(CellDataStyle).Element(c => c.Text((string)ligne.Libelle));
                                 table.Cell().Element(CellDataStyle).Element(c => c.Text((string)ligne.Debit));
                                 table.Cell().Element(CellDataStyle).Element(c => c.Text((string)ligne.Credit));
                             }

                             static IContainer CellDataStyle(IContainer container) => container.Padding(5);
                         });

                         content.Item().PaddingTop(20).AlignLeft().Text($"Solde actuel au {dateActuelle} : {solde:F3} TND").Bold().FontSize(12);
                     });
                 });
             });

             return document.GeneratePdf();
         }*/


    }








}


