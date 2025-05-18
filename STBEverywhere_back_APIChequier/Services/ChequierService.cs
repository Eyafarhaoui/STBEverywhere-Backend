using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using STBEverywhere_back_APIChequier.Controllers;
using STBEverywhere_back_APIChequier.Hubs;
using STBEverywhere_back_APIChequier.Repository.IRepositoy;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;
using System.Net.Http;
using System.Numerics;
using System.Text;

namespace STBEverywhere_back_APIChequier.Services
{
    public class ChequierService
    {

        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ChequierService> _logger;
        private readonly IChequierRepository _chequierRepository;
        private readonly IEmailLogRepository _emailLogRepository;
        private readonly HttpClient _httpClient;
        public ChequierService(HttpClient httpClient, IChequierRepository chequierRepository, IEmailLogRepository emailLogRepository, ILogger<ChequierService> logger, ApplicationDbContext context, EmailService emailService, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _emailService = emailService;
            _hubContext = hubContext;
            _logger = logger;
            _chequierRepository = chequierRepository ?? throw new ArgumentNullException(nameof(chequierRepository));
            _emailLogRepository = emailLogRepository ?? throw new ArgumentNullException(nameof(emailLogRepository));
            _httpClient = httpClient;

        }




        public async Task VérifierChéquiersExpedieAsync()
        {
            var chequiers = await _chequierRepository.GetChequiersExpedieAsync() ?? new List<DemandeChequier>();

            foreach (var chequier in chequiers)
            {
                var existingEmailLog = await _emailLogRepository.GetExistingEmailLogAsync(chequier.IdDemande, "expedie");

                if (existingEmailLog == null) // Pas encore envoyé
                {
                    var sujet = "Acheminement de votre chéquier – Confirmation d’expédition";
                   
                    //string salutation = chequier?.Compte?.Client?.Genre == "Féminin" ? "Madame" : "Monsieur";

                    //string nomComplet = $"{chequier?.Compte?.Client?.Prenom} {chequier?.Compte?.Client?.Nom}";
                    var contenu = $@"
 <p><strong>Bonjour,</strong></p>


<p>Nous avons le plaisir de vous informer que votre <strong>chéquier</strong> a été expédié par courrier recommandé. Celui-ci est actuellement en cours d’acheminement à l’adresse postale que vous avez renseignée lors de votre demande.</p>

<p>Nous vous invitons à vérifier la disponibilité de cette adresse afin d'assurer une bonne réception du courrier. En cas de non-réception dans un délai raisonnable, nous vous prions de bien vouloir prendre contact avec votre agence.</p>

<p>Nous vous remercions pour la confiance que vous accordez à notre établissement.</p>

<p>Cordialement,<br>
<strong>STB – Département de la Gestion des Moyens de Paiement</strong></p>

<p style='font-size: small; color: gray;'>
<i>Ce message a été généré automatiquement. Merci de ne pas y répondre.</i>
</p>";

                    // Appel HTTP vers EmailController
                    var emailRequest = new
                    {
                        to = chequier.Email,
                        subject = sujet,
                        content = $"<p>{contenu.Replace("\n", "<br>")}</p>"
                    };

                    var emailResponse = await _httpClient.PostAsJsonAsync("http://localhost:5203/api/Email/send", emailRequest);

                    if (emailResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Email expédié avec succès à {Email}", chequier.Email);
                        await _emailService.LogEmailAsync(chequier.Email, sujet, contenu, chequier.IdDemande, "expedie");
                    }
                    else
                    {
                        _logger.LogError("Erreur lors de l'envoi de l'email à {Email}", chequier.Email);
                    }
                }

                _logger.LogInformation("Envoi de notification à l'email {Email}.", chequier.Email);

               
            }
        }






        public async Task VérifierChequiersDisponibleEnAgenceAsync()
        {
            var demandesDispo = await _context.DemandesChequiers
                .Where(d => d.Status == DemandeStatus.DisponibleEnAgence && d.ModeLivraison == ModeLivraison.LivraisonAgence)
                .ToListAsync();

            foreach (var demande in demandesDispo)
            {
                var existingChequier = await _context.Chequiers
                    .FirstOrDefaultAsync(c => c.DemandeChequierId == demande.IdDemande);

                if (existingChequier == null)
                {
                    var chequier = new Chequier
                    {
                        DemandeChequierId = demande.IdDemande,
                        Status = ChequierStatus.Actif,
                        DateLivraison = DateTime.Now,
                    };
                   

                    _context.Chequiers.Add(chequier);
                    await _context.SaveChangesAsync();

                    var existingEmailLog = await _context.EmailLogs
                        .FirstOrDefaultAsync(e =>
                            e.DemandeId == chequier.DemandeChequierId &&
                            e.IsEnvoye &&
                            e.EmailType == "cheque dispo");

                    if (existingEmailLog == null)
                    {
                        //string salutation = chequier?.DemandeChequier?.Compte?.Client?.Genre == "Féminin" ? "Madame" : "Monsieur";

                        //string nomComplet = $"{chequier?.DemandeChequier?.Compte?.Client?.Prenom} {chequier?.DemandeChequier?.Compte?.Client?.Nom}";
                        //Console.WriteLine("nom complet oiur chequier dispo agen", nomComplet);
                        var contenu = @"
 <p><strong>Bonjour,</strong></p>


<p>Nous vous informons que votre <strong>demande de chéquier</strong> a été <strong>traitée avec succès</strong>.</p>

<p>Votre chéquier est désormais disponible dans votre agence. Vous pouvez venir le retirer à tout moment pendant les horaires d'ouverture habituels.</p>

<p>Si vous avez la moindre question, n’hésitez pas à contacter votre conseiller ou notre service client.</p>

<p>Cordialement,<br>
<strong>Le Service Client</strong><br>
Société Tunisienne de Banque</p>

<p style='font-size: small; color: gray;'>
<i>Ce message a été généré automatiquement. Merci de ne pas y répondre.</i>
</p>";


                        // Appel HTTP vers EmailController
                        var emailRequest = new
                        {
                            to = demande.Email,
                            subject = "Votre chéquier est disponible en Agence",
                            content = contenu
                        };

                        var emailResponse = await _httpClient.PostAsJsonAsync("http://localhost:5203/api/Email/send", emailRequest);

                        if (emailResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Email envoyé avec succès à {Email}", demande.Email);
                            await _emailService.LogEmailAsync(demande.Email, "Votre chéquier est disponible en Agence", contenu, demande.IdDemande, "cheque dispo");
                        }
                        else
                        {
                            _logger.LogError("Erreur lors de l'envoi de l'email à {Email}", demande.Email);
                        }
                    }

                    _logger.LogInformation("Envoi de notification pour le chéquier {ChequierId} à l'email {Email}.", chequier.Id, demande.Email);

                    await _hubContext.Clients.User(demande.Email)
                        .SendAsync("ReceiveNotification", $"Votre chéquier est livré. Le numéro de votre chéquier");
                }
                else
                {
                    _logger.LogInformation("Le chéquier pour la demande {DemandeId} existe déjà. Aucun ajout effectué.", demande.IdDemande);
                }
            }
        }

    }
}
        
