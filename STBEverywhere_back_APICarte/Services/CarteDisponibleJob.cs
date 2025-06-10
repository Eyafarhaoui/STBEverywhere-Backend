using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using STBEverywhere_Back_SharedModels.Models.enums;
using System.Text;
using System.Text.Json;

namespace STBEverywhere_back_APICarte.Services
{
    public class CarteDisponibleJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CarteDisponibleJob> _logger;

        public CarteDisponibleJob(IServiceProvider serviceProvider, ILogger<CarteDisponibleJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CarteDisponibleJob démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var carteService = scope.ServiceProvider.GetRequiredService<ICarteService>();

                        // Récupérer les demandes avec le statut "DisponibleEnAgence" et où l'email n'a pas encore été envoyé
                        var demandesDisponibles = await carteService.GetDemandesByStatutAsync(StatutDemande.DisponibleEnAgence);

                        _logger.LogInformation("Nombre de demandes disponibles à l'agence : {Count}", demandesDisponibles?.Count() ?? 0);

                        if (demandesDisponibles != null)
                        {
                            foreach (var demande in demandesDisponibles)
                            {
                                try
                                {
                                    // Vérifier si l'email a déjà été envoyé
                                    if (demande.EmailEnvoye)
                                    {
                                        _logger.LogInformation("Email déjà envoyé pour la demande : {DemandeId}", demande.Iddemande);
                                        continue;
                                    }

                                    // Vérifier si l'email est vide
                                    if (string.IsNullOrWhiteSpace(demande.Email))
                                    {
                                        _logger.LogWarning("Email vide pour la demande : {DemandeId}", demande.Iddemande);
                                        continue;
                                    }




                                    var emailRequest = new
                                    {
                                        to = demande.Email,
                                        subject = "Votre carte est disponible à l'agence",
                                        content = $@"
                                            

                                            <p>Nous avons le plaisir de vous informer que votre carte <strong>{demande.NomCarte}</strong> est maintenant disponible dans votre agence.</p>

                                            <p><strong>Détails de votre carte :</strong></p>
                                            <ul>
                                                <li>Type de carte : {demande.TypeCarte}</li>
                                                
                                            </ul>

                                            <p>Vous pouvez venir la retirer à l'agence  aux horaires d'ouverture, muni(e) de votre pièce d'identité.</p>

                                            
                                            <p>Nous vous remercions pour la confiance que vous accordez à notre établissement.</p>

                                            <p>Cordialement,<br>
                                            <strong>STB – Département de la Gestion des Moyens de Paiement</strong></p>

                                            <p style='font-size: small; color: gray;'>
                                            <i>Ce message a été généré automatiquement. Merci de ne pas y répondre.</i></p>"
                                    };

                                    using (var httpClient = new HttpClient())
                                    {
                                        var json = JsonSerializer.Serialize(emailRequest);
                                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                                        _logger.LogInformation("Envoi d'email pour la demande : {DemandeId}", demande.Iddemande);
                                        _logger.LogDebug("Destinataire : {To}", emailRequest.to);
                                        _logger.LogDebug("Sujet : {Subject}", emailRequest.subject);

                                        var response = await httpClient.PostAsync(
                                            "http://localhost:5203/api/email/send",
                                            content);

                                        if (response.IsSuccessStatusCode)
                                        {
                                            await carteService.UpdateEmailEnvoyeAsync(demande.Iddemande, true);
                                            _logger.LogInformation("Email envoyé avec succès pour la demande : {DemandeId}", demande.Iddemande);
                                        }
                                        else
                                        {
                                            var errorContent = await response.Content.ReadAsStringAsync();
                                            _logger.LogError("Erreur lors de l'envoi de l'email pour la demande {DemandeId} : {StatusCode} - {Error}",
                                                demande.Iddemande, response.StatusCode, errorContent);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Erreur lors du traitement de la demande {DemandeId}", demande.Iddemande);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Une erreur s'est produite lors de l'exécution du job.");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}