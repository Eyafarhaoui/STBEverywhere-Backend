using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using STBEverywhere_Back_SharedModels.Models.enums;
using System.Text;
using System.Text.Json;
using STBEverywhere_Back_SharedModels;

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

                        var demandesDisponibles = await carteService.GetDemandesByStatutAsync(StatutDemande.DisponibleEnAgence);

                        _logger.LogInformation("Nombre de demandes disponibles à l'agence : {Count}", demandesDisponibles?.Count() ?? 0);

                        if (demandesDisponibles != null)
                        {
                            foreach (var demande in demandesDisponibles)
                            {
                                if (demande == null) continue;

                                // Vérifier si l'email a déjà été envoyé
                                if (demande.EmailEnvoye)
                                {
                                    _logger.LogInformation("Email déjà envoyé pour la demande : {DemandeId}", demande.Iddemande);
                                    continue;
                                }

                                try
                                {
                                    if (string.IsNullOrWhiteSpace(demande.Email))
                                    {
                                        _logger.LogWarning("Email vide pour la demande : {DemandeId}", demande.Iddemande);
                                        continue;
                                    }

                                    if (demande.Compte?.Client == null)
                                    {
                                        _logger.LogWarning("Client ou Compte manquant pour la demande : {DemandeId}", demande.Iddemande);
                                        continue;
                                    }

                                    _logger.LogInformation("Envoi d'email pour la demande : {DemandeId}", demande.Iddemande);
                                    string salutation = demande?.Compte?.Client?.Genre == "Féminin" ? "Madame" : "Monsieur";
                                    string nomComplet = $"{demande?.Compte?.Client?.Prenom} {demande?.Compte?.Client?.Nom}";

                                    // Construire l'objet email à envoyer
                                    var emailRequest = new
                                    {
                                        to = demande.Email,
                                        subject = "Votre carte est disponible à l'agence",
                                        content = $@" 
<p><strong>{salutation} {nomComplet},</strong></p>

<p>Nous avons le plaisir de vous informer que votre <strong>chéquier</strong> a été expédié par courrier recommandé. Celui-ci est actuellement en cours d'acheminement à l'adresse postale que vous avez renseignée lors de votre demande.</p>

<p>Nous vous invitons à vérifier la disponibilité de cette adresse afin d'assurer une bonne réception du courrier. En cas de non-réception dans un délai raisonnable, nous vous prions de bien vouloir prendre contact avec votre agence.</p>

<p>Nous vous remercions pour la confiance que vous accordez à notre établissement.</p>

<p>Cordialement,<br>
<strong>STB – Département de la Gestion des Moyens de Paiement</strong></p>

<p style='font-size: small; color: gray;'>
<i>Ce message a été généré automatiquement. Merci de ne pas y répondre.</i>
</p>"
                                    };

                                    using (var httpClient = new HttpClient())
                                    {
                                        var json = JsonSerializer.Serialize(emailRequest);
                                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                                        _logger.LogInformation("Appel de l'envoi d'email à {To} avec sujet {Subject}", demande.Email, emailRequest.subject);
                                        _logger.LogDebug("Body email : {Body}", emailRequest.content);

                                        var response = await httpClient.PostAsync("http://localhost:5203/api/email/send", content, stoppingToken);

                                        if (response.IsSuccessStatusCode)
                                        {
                                            // Mettre à jour le champ EmailEnvoye
                                            await carteService.UpdateEmailEnvoyeAsync(demande.Iddemande, true);
                                            _logger.LogInformation("Email envoyé avec succès pour la demande : {DemandeId}", demande.Iddemande);
                                        }
                                        else
                                        {
                                            var errorContent = await response.Content.ReadAsStringAsync();
                                            _logger.LogError("Erreur lors de l'envoi de l'email pour la demande {DemandeId} : {Error}", demande.Iddemande, errorContent);
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




        /* protected override async Task ExecuteAsync(CancellationToken stoppingToken)
         {
             _logger.LogInformation("CarteDisponibleJob démarré.");

             while (!stoppingToken.IsCancellationRequested)
             {
                 try
                 {
                     using (var scope = _serviceProvider.CreateScope())
                     {
                         var carteService = scope.ServiceProvider.GetRequiredService<ICarteService>();

                         var demandesDisponibles = await carteService.GetDemandesByStatutAsync(StatutDemande.DisponibleEnAgence);

                         _logger.LogInformation("Nombre de demandes disponibles à l'agence : {Count}", demandesDisponibles.Count());

                         foreach (var demande in demandesDisponibles)
                         {
                             // Vérifier si l'email a déjà été envoyé
                             if (demande.EmailEnvoye)
                             {
                                 _logger.LogInformation("Email déjà envoyé pour la demande : {DemandeId}", demande.Iddemande);
                                 continue; // Passer à la demande suivante
                             }

                             if (!demande.EmailEnvoye)
                             {
                                 _logger.LogInformation("Envoi d'email pour la demande : {DemandeId}", demande.Iddemande);

   string salutation = demande.Compte.Client.Genre == "Féminin" ? "Madame" : "Monsieur";
                                 string nomComplet = $"{demande.Compte.Client.Prenom} {demande.Compte.Client.Nom}";
                                 // Construire l'objet email à envoyer
                                 var emailRequest = new
                                 {

                                 to = demande.Email,
                                     subject = "Votre carte est disponible à l'agence",
                                     content = @" 
 <p><strong>Madame, Monsieur,</strong></p>

 <p>Nous avons le plaisir de vous informer que votre <strong>chéquier</strong> a été expédié par courrier recommandé. Celui-ci est actuellement en cours d’acheminement à l’adresse postale que vous avez renseignée lors de votre demande.</p>

 <p>Nous vous invitons à vérifier la disponibilité de cette adresse afin d'assurer une bonne réception du courrier. En cas de non-réception dans un délai raisonnable, nous vous prions de bien vouloir prendre contact avec votre agence.</p>

 <p>Nous vous remercions pour la confiance que vous accordez à notre établissement.</p>

 <p>Cordialement,<br>
 <strong>STB – Département de la Gestion des Moyens de Paiement</strong></p>

 <p style='font-size: small; color: gray;'>
 <i>Ce message a été généré automatiquement. Merci de ne pas y répondre.</i>
 </p>",

                             };

                                 using (var httpClient = new HttpClient())
                                 {
                                     var json = JsonSerializer.Serialize(emailRequest);
                                     var content = new StringContent(json, Encoding.UTF8, "application/json");

                                     _logger.LogInformation("Appel de l'envoi d'email à {To} avec sujet {Subject}", demande.Email, emailRequest.subject);
                                     _logger.LogInformation("Body email : {Body}", emailRequest.content);

                                     var response = await httpClient.PostAsync("http://localhost:5203/api/email/send", content);

                                     if (response.IsSuccessStatusCode)
                                     {
                                         // Mettre à jour le champ EmailEnvoye
                                         await carteService.UpdateEmailEnvoyeAsync(demande.Iddemande, true);

                                         _logger.LogInformation("Email envoyé pour la demande : {DemandeId}", demande.Iddemande);
                                     }
                                     else
                                     {
                                         var errorContent = await response.Content.ReadAsStringAsync();
                                         _logger.LogError("Erreur lors de l'envoi de l'email pour la demande {DemandeId} : {Error}", demande.Iddemande, errorContent);
                                         // Tu peux décider de throw une exception ou juste continuer
                                     }
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
     }*/
    }
}