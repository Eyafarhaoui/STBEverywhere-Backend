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
    public class CarteLivreeJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CarteLivreeJob> _logger;

        public CarteLivreeJob(IServiceProvider serviceProvider, ILogger<CarteLivreeJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CarteLivreeJob démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var carteService = scope.ServiceProvider.GetRequiredService<ICarteService>();

                        // Récupérer les demandes avec le statut "Livrée" et où l'email n'a pas encore été envoyé
                        var demandesLivrees = await carteService.GetDemandesByStatutAsync(StatutDemande.Livree);

                        _logger.LogInformation("Nombre de demandes livrées : {Count}", demandesLivrees.Count());

                        foreach (var demande in demandesLivrees)
                        {
                            // Vérifier si l'email a déjà été envoyé
                            if (demande.EmailEnvoyeLivree)
                            {
                                _logger.LogInformation("Email déjà envoyé pour la demande : {DemandeId}", demande.Iddemande);
                                continue; // Passer à la demande suivante
                            }

                            // Vérifier si l'email est à false
                            if (!demande.EmailEnvoyeLivree)
                            {
                                _logger.LogInformation("Envoi d'email pour la demande : {DemandeId}", demande.Iddemande);

                                // Préparer l'objet email à envoyer
                                var emailRequest = new
                                {
                                    to = demande.Email,
                                    subject = "Votre carte est livrée",
                                    content = $"Votre carte {demande.NomCarte} a été livrée avec succès."
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
                                        // Mettre à jour le champ EmailEnvoyeLivree
                                        await carteService.UpdateEmailEnvoyeLivreeAsync(demande.Iddemande, true);

                                        _logger.LogInformation("Email envoyé pour la demande : {DemandeId}", demande.Iddemande);
                                    }
                                    else
                                    {
                                        var errorContent = await response.Content.ReadAsStringAsync();
                                        _logger.LogError("Erreur lors de l'envoi de l'email pour la demande {DemandeId} : {Error}", demande.Iddemande, errorContent);
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