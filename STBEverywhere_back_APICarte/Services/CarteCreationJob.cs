using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels.Models.enums;
using STBEverywhere_back_APICompte.Services.IServices;

namespace STBEverywhere_back_APICarte.Services
{
    public class CarteCreationJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CarteCreationJob> _logger;

        // Dictionnaire pour mapper les types de carte aux frais correspondants
        private readonly Dictionary<NomCarte, decimal> _fraisCartes = new()
        {
            { NomCarte.VisaClassic, 35 },        // Visa Classic Nationale
            { NomCarte.Mastercard, 35 },          // Par défaut, même prix que Visa Classic
            { NomCarte.Tecno, 15 },               // Carte Technologique Internationale Particulier
            { NomCarte.VisaPlatinum, 150 },       // Visa Platinum Business Nationale
            { NomCarte.VisaInfinite, 200 },       // Visa Platinum Business Internationale
            { NomCarte.MastercardGold, 90 },      // MasterCard Gold Nationale
            { NomCarte.C_cash, 10 },
            { NomCarte.C_pay, 10 },// MasterCard Gold Internationale
            { NomCarte.CIB, 20 },            // Carte CIB
            { NomCarte.Epargne, 0 }                 // Gratuit
        };

        public CarteCreationJob(IServiceProvider serviceProvider, ILogger<CarteCreationJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CarteCreationJob démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var carteService = scope.ServiceProvider.GetRequiredService<ICarteService>();
                        var compteService = scope.ServiceProvider.GetRequiredService<ICompteService>();

                        var demandesRecuperees = await carteService.GetDemandesByStatutAsync(StatutDemande.Recuperee);

                        _logger.LogInformation("Nombre de demandes récupérées : {Count}", demandesRecuperees.Count());

                        foreach (var demande in demandesRecuperees)
                        {
                            _logger.LogInformation("Traitement de la demande : {DemandeId}", demande.Iddemande);

                            if (demande.CarteAjouter)
                            {
                                _logger.LogInformation("Une carte a déjà été ajoutée pour la demande : {DemandeId}", demande.Iddemande);
                                continue;
                            }

                            // Créer la carte
                            var  carteCreee = await carteService.CreateCarteIfDemandeRecupereeAsync(demande.Iddemande);

                            if (carteCreee != null)
                            {
                                // Récupérer le type de carte pour déterminer les frais
                                var fraisMontant = GetFraisForCarte(carteCreee.NomCarte);

                                if (fraisMontant > 0)
                                {
                                    // Ajouter les frais de carte
                                    var fraisCarte = new FraisCarte
                                    {
                                        Type = TypeFraisCarte.Creation.ToString(),
                                        Date = DateTime.Now,
                                        Montant = fraisMontant,
                                        NumCarte = carteCreee.NumCarte
                                    };

                                    await carteService.AddFraisToCarte(carteCreee.NumCarte, fraisCarte);

                                    // Déduire les frais du compte client
                                    var compte = await compteService.GetByRIBAsync(demande.NumCompte);
                                    if (compte != null)
                                    {
                                        compte.Solde -= fraisMontant;
                                        await compteService.UpdateAsync(compte);

                                        _logger.LogInformation("Frais de {Montant} déduits du compte {RIB} pour la carte {NumCarte}",
                                            fraisMontant, demande.NumCompte,carteCreee.NumCarte);
                                    }
                                }

                                // Mettre à jour le champ CarteAjouter
                                demande.CarteAjouter = true;
                                await carteService.UpdateDemandeAsync(demande);

                                _logger.LogInformation("Carte créée, frais ajoutés et demande mise à jour pour : {DemandeId}", demande.Iddemande);
                            }
                            else
                            {
                                _logger.LogWarning("Échec de la création de la carte pour la demande : {DemandeId}", demande.Iddemande);
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

        private decimal GetFraisForCarte(NomCarte nomCarte)
        {
            if (_fraisCartes.TryGetValue(nomCarte, out var frais))
            {
                return frais;
            }
            return 0; // Par défaut, pas de frais si la carte n'est pas dans le dictionnaire
        }

       
    }
}