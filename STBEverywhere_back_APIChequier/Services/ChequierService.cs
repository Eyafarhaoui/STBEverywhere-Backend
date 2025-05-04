using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIChequier.Controllers;
using STBEverywhere_back_APIChequier.Hubs;
using STBEverywhere_back_APIChequier.Repository.IRepositoy;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;

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
        public ChequierService(IChequierRepository chequierRepository,IEmailLogRepository emailLogRepository, ILogger<ChequierService> logger, ApplicationDbContext context, EmailService emailService, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _emailService = emailService;
            _hubContext = hubContext;
            _logger = logger;
            _chequierRepository = chequierRepository ?? throw new ArgumentNullException(nameof(chequierRepository));
            _emailLogRepository = emailLogRepository ?? throw new ArgumentNullException(nameof(emailLogRepository));

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
                    var contenu = @"Madame, Monsieur,

Nous avons le plaisir de vous informer que votre chéquier a été  a été envoyé par courrier recommandé. Celui-ci est actuellement en cours d’expédition à l’adresse postale communiquée lors de la saisie de votre demande.

Nous vous invitons à vous assurer de la disponibilité de cette adresse pour la bonne réception de votre chéquier. En cas de non-réception dans un délai raisonnable, nous vous prions de bien vouloir contacter votre agence.

Nous vous remercions pour la confiance que vous accordez à notre établissement.

Cordialement,
STB – Département de la Gestion des Moyens de Paiement";

                    await _emailService.LogEmailAsync(chequier.Email, sujet, contenu, chequier.IdDemande, "expedie");
                }

                //_logger.LogInformation("Envoi de notification pour le chéquier {ChequierId} à l'email {Email}.", chequier.NumeroChequier, chequier.Email);
                _logger.LogInformation("Envoi de notification à l'email {Email}.", chequier.Email);

                // Envoyer une notification en temps réel avec SignalR
                await _hubContext.Clients.User(chequier.Email)
                    .SendAsync("ReceiveNotification", "Votre chéquier a été expédié et est en cours de livraison.");
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
                        var contenu = $"Nous vous informons que votre demande de chéquier a été traitée avec succès et que votre chéquier est désormais disponible dans l'agence . Vous pouvez venir le retirer à tout moment pendant les horaires d'ouverture de l'agence.\r\n\r\nSi vous avez des questions, n'hésitez pas à nous contacter.\r\nCordialement,\r\nSTB";

                        await _emailService.LogEmailAsync(demande.Email, "Votre chéquier est disponile en Agence ", contenu, demande.IdDemande, "cheque dispo");
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
