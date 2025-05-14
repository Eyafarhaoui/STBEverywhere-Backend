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

                            // Vérifier si l'email est à false
                            if (!demande.EmailEnvoye)
                            {
                                _logger.LogInformation("Envoi d'email pour la demande : {DemandeId}", demande.Iddemande);

                                // Construire l'objet email à envoyer
                                var emailRequest = new
                                {
                                    to = demande.Email,
                                    subject = "Votre carte est disponible à l'agence",
                                    content = $"Votre carte {demande.NomCarte} est disponible à l'agence."
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
    }
}