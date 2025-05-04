using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIChequier.Hubs;
using STBEverywhere_back_APIChequier.Services;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIChequier.Jobs
{
    public class ChequierDisponibleEnAgenceJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ChequierDisponibleEnAgenceJob> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public ChequierDisponibleEnAgenceJob(IServiceProvider serviceProvider, ILogger<ChequierDisponibleEnAgenceJob> logger, IHubContext<NotificationHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hubContext = hubContext;
            ;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var ChequierService = scope.ServiceProvider.GetRequiredService<ChequierService>();

                        await ChequierService.VérifierChequiersDisponibleEnAgenceAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de la vérification des demandes de chéquiers disponibles en agences.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }


        /*protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                        // Récupérer les demandes dont le statut est 'livré'
                        var demandesDispo = await context.DemandesChequiers
                            .Where(d => d.Status == DemandeStatus.DisponibleEnAgence && d.ModeLivraison == ModeLivraison.LivraisonAgence) 
                            .ToListAsync();

                        foreach (var demande in demandesDispo) // Ajout de la boucle foreach
                        {
                            // Vérifier si un chéquier existe déjà pour cette demande
                            var existingChequier = await context.Chequiers
                                .FirstOrDefaultAsync(c => c.DemandeChequierId == demande.IdDemande);

                            if (existingChequier == null) // Si aucun chéquier n'existe pour cette demande
                            {
                                var chequier = new Chequier
                                {
                                    DemandeChequierId = demande.IdDemande,
                                    Status = ChequierStatus.Actif,
                                    DateLivraison = DateTime.Now, // Marquer la date de livraison
                                };

                                // Ajouter le chéquier à la table
                                context.Chequiers.Add(chequier);
                                await context.SaveChangesAsync();

                                var existingEmailLog = await context.EmailLogs
                                    .Where(e => e.DemandeId == chequier.DemandeChequierId && e.IsEnvoye && e.EmailType == "cheque dispo")
                                    .FirstOrDefaultAsync();

                                if (existingEmailLog == null) // Si l'email n'a pas encore été envoyé
                                {
                                    //var contenu = $"Nous vous informons que votre demande de chéquier a été traitée avec succès et que votre chéquier {demande.NumeroChequier} est désormais disponible dans l'agence {demande.Agence}. Vous pouvez venir le retirer à tout moment pendant les horaires d'ouverture de l'agence.\r\n\r\nSi vous avez des questions, n'hésitez pas à nous contacter.\r\nCordialement,\r\nSTB";
                                    var contenu = $"Nous vous informons que votre demande de chéquier a été traitée avec succès et que votre chéquier {demande.NumeroChequier} est désormais disponible dans l'agence . Vous pouvez venir le retirer à tout moment pendant les horaires d'ouverture de l'agence.\r\n\r\nSi vous avez des questions, n'hésitez pas à nous contacter.\r\nCordialement,\r\nSTB";

                                    await emailService.LogEmailAsync(demande.Email, "Votre chéquier est disponile en Agence ", contenu, demande.IdDemande, "cheque dispo");
                                }

                                _logger.LogInformation("Envoi de notification pour le chéquier {ChequierId} à l'email {Email}.", chequier.Id, demande.Email);
                                await _hubContext.Clients.User(demande.Email).SendAsync("ReceiveNotification", $"Votre chéquier est livré. Le numéro de votre chéquier est {demande.NumeroChequier}.");
                            }
                            else
                            {
                                _logger.LogInformation("Le chéquier pour la demande {DemandeId} existe déjà. Aucun ajout effectué.", demande.IdDemande);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de la vérification des demandes de chéquiers disponibles en agences.");
                }

                // Vérifier toutes les minutes (ou ajuster la fréquence selon votre besoin)
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }*/
    }
}
