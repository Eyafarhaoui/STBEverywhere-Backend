
using STBEverywhere_back_APICarte.Repository;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_Back_SharedModels.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Security.Claims;
using STBEverywhere_Back_SharedModels.Models.enums;
using STBEverywhere_back_APICompte.Services.IServices;

namespace STBEverywhere_back_APICarte.Services
{
    public class CarteService : ICarteService
    {
        private readonly ICarteRepository _carteRepository;
        private readonly ILogger<CarteService> _logger;
        private readonly EmailService _emailService;
        private readonly ApplicationDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ICompteService _compteService; // Ajout du service Compte

        public CarteService(
            ICarteRepository carteRepository,
            HttpClient httpClient,
            ILogger<CarteService> logger,
            EmailService emailService,
            ApplicationDbContext dbContext,
            ICompteService compteService) // Injection du service
        {
            _carteRepository = carteRepository;
            _logger = logger;
            _emailService = emailService;
            _dbContext = dbContext;
            _httpClient = httpClient;
            _compteService = compteService; // Initialisation
        }

        public async Task<IEnumerable<CarteDTO>> GetCartesByRIBAsync(string rib)
        {
            _logger.LogInformation("Récupération des cartes pour le RIB : {RIB}", rib);
            var cartes = await _carteRepository.GetCartesByRIBAsync(rib);
            return cartes.Select(c => new CarteDTO
            {
                NumCarte = c.NumCarte,
                NomCarte = c.NomCarte,
                TypeCarte = c.TypeCarte,
                DateCreation = c.DateCreation,
                DateExpiration = c.DateExpiration,
                Statut = c.Statut,
                RIB = c.RIB
            });
        }
        public async Task<IEnumerable<CarteDTO>> GetCartesByClientIdAsync(int clientId)
        {
            var cartes = await _dbContext.Cartes
                .Include(c => c.Compte) // Inclure le compte associé
                .Where(c => c.Compte.ClientId == clientId) // Filtrer par ClientId
                .Select(c => new CarteDTO
                {
                    NumCarte = c.NumCarte,
                    TypeCarte = c.TypeCarte,
                    NomCarte = c.NomCarte,
                    DateExpiration = c.DateExpiration,
                    Statut = c.Statut,
                    RIB = c.RIB,
                    Solde = c.Solde // Ajout du solde si nécessaire
                })
                .ToListAsync();

            return cartes;
        }


        // Dans CarteService.cs - modifiez la méthode CreateCarteIfDemandeRecupereeAsync
        public async Task<Carte> CreateCarteIfDemandeRecupereeAsync(int demandeId)
        {
            _logger.LogInformation("Tentative de création de carte pour la demande : {DemandeId}", demandeId);

            var demande = await _carteRepository.GetDemandeCarteByIdAsync(demandeId);
            if (demande == null)
            {
                _logger.LogWarning("Demande de carte introuvable : {DemandeId}", demandeId);
                throw new InvalidOperationException("Demande de carte introuvable.");
            }

            if (demande.Statut != StatutDemande.Recuperee)
            {
                _logger.LogWarning("La carte n'est pas encore récupérée : {DemandeId}", demandeId);
                throw new InvalidOperationException("La carte ne peut être créée que si la demande est récupérée.");
            }

            var codePIN = await GenerateUniquePinAsync();
            var codeCVV = await GenerateUniqueCvvAsync();
            var encryptedPIN = EncryptCode(int.Parse(codePIN));
            var encryptedCVV = EncryptCode(int.Parse(codeCVV));
            var numCarte = await GenerateUniqueCardNumberAsync(demande.NomCarte.ToString());
            var compte = await _compteService.GetByRIBAsync(demande.NumCompte);
            var carte = new Carte
            {
                NumCarte = numCarte,
                NomCarte = demande.NomCarte,
                TypeCarte = demande.TypeCarte,
                DateCreation = demande.DateCreation,
                DateExpiration = demande.DateCreation.AddYears(3),
                Statut = StatutCarte.Active,
                RIB = demande.NumCompte,
                Solde = compte.Solde,
                PlafondTPE = 4000,
                PlafondDAP = 2000,
                Iddemande = demande.Iddemande,
                DateRecuperation = DateTime.Now,
                CodePIN = encryptedPIN,
                CodeCVV = encryptedCVV
            };

            var result = await _carteRepository.CreateCarteAsync(carte);

            if (!result)
            {
                _logger.LogError("Échec de la création de la carte pour la demande : {DemandeId}", demandeId);
                return null;
            }

            return carte;
        }

        // Ajoutez cette nouvelle méthode
        public async Task AddFraisToCarte(string numCarte, FraisCarte frais)
        {
            await _carteRepository.AddFraisToCarteAsync(numCarte, frais);
        }

        private string EncryptCode(int code)
        {
            // Convertir le code en chaîne de caractères
            string codeString = code.ToString();

            // Crypter le code avec BCrypt
            return BCrypt.Net.BCrypt.HashPassword(codeString);
        }

        public async Task<IEnumerable<DemandeCarte>> GetDemandesByStatutAsync(StatutDemande statut)
        {
            _logger.LogInformation("Récupération des demandes avec le statut : {Statut}", statut);

            // Récupérer les demandes avec le statut spécifié
            var demandes = await _carteRepository.GetDemandesByStatutAsync(statut);

            _logger.LogInformation("Nombre de demandes récupérées : {Count}", demandes.Count());

            // Log des détails des demandes récupérées
            foreach (var demande in demandes)
            {
                _logger.LogInformation("Détails de la demande : Id={Iddemande}, NumCompte={NumCompte}, NomCarte={NomCarte}, TypeCarte={TypeCarte}, Statut={Statut}",
                    demande.Iddemande, demande.NumCompte, demande.NomCarte, demande.TypeCarte, demande.Statut);
            }

            return demandes;
        }

        private async Task<string> GenerateUniqueCardNumberAsync(string nomCarte)
        {
            string cardNumber;
            bool isUnique;

            do
            {
                // Générer un numéro de carte en fonction du type de carte
                cardNumber = GenerateCardNumberWithPrefix(nomCarte);

                // Vérifier si le numéro de carte existe déjà dans la base de données
                isUnique = !await _carteRepository.CardNumberExistsAsync(cardNumber);
            } while (!isUnique); // Répéter jusqu'à ce qu'un numéro unique soit généré

            return cardNumber;
        }

        private string GenerateCardNumberWithPrefix(string nomCarte)
        {
            string prefix;
            int length;

            // Définir le préfixe et la longueur en fonction du type de carte
            if (nomCarte.Contains("Visa"))
            {
                prefix = "431405";
                length = 16; // Longueur totale de la carte Visa
            }
            else if (nomCarte.Contains("CIB"))
            {
                prefix = "539997";
                length = 16; // Longueur totale de la carte Visa
            }
            else if (nomCarte.Contains("C_"))
            {
                prefix = "4906012";
                length = 16; // Longueur totale de la carte Visa
            }

            else if (nomCarte.Contains("Mastercard"))
            {
                prefix = "539997";
                length = 16; // Longueur totale de la Mastercard
            }
            else if (nomCarte.Contains("Epargne"))
            {
                prefix = "4906001";
                length = 16; // Longueur totale de la carte Épargne
            }
            else
            {
                throw new ArgumentException("Type de carte non reconnu.");
            }

            // Générer les chiffres restants après le préfixe
            var random = new Random();
            var cardNumberBuilder = new StringBuilder(prefix);

            // Ajouter des chiffres aléatoires jusqu'à atteindre la longueur totale
            while (cardNumberBuilder.Length < length)
            {
                cardNumberBuilder.Append(random.Next(0, 10)); // Chiffre entre 0 et 9
            }

            return cardNumberBuilder.ToString();
        }

        private async Task<string> GenerateUniquePinAsync()
        {
            string pin;
            bool isUnique;

            do
            {
                pin = GenerateRandomCode(4); // Générer un code PIN à 4 chiffres sous forme de string
                isUnique = !await _carteRepository.PinExistsAsync(pin); // Vérifier l'unicité
            } while (!isUnique);

            return pin;
        }

        private async Task<string> GenerateUniqueCvvAsync()
        {
            string cvv;
            bool isUnique;

            do
            {
                cvv = GenerateRandomCode(3); // Générer un code CVV à 3 chiffres sous forme de string
                isUnique = !await _carteRepository.CvvExistsAsync(cvv); // Vérifier l'unicité
            } while (!isUnique);

            return cvv;
        }

        // Modifier GenerateRandomCode pour retourner une string
        private string GenerateRandomCode(int length)
        {
            Random random = new Random();
            return new string(Enumerable.Range(0, length)
                .Select(_ => (char)('0' + random.Next(0, 10)))
                .ToArray());
        }

        public async Task<IEnumerable<DemandeCarteDTO>> GetDemandesByRIBAsync(string rib)
        {
            _logger.LogInformation("Récupération des demandes de carte pour le RIB : {RIB}", rib);
            var demandes = await _carteRepository.GetDemandesByRIBAsync(rib);

            var demandesDTO = demandes.Select(d => new DemandeCarteDTO
            {
                NumCompte = d.NumCompte,
                NomCarte = d.NomCarte,
                TypeCarte = d.TypeCarte,
                CIN = d.CIN,
                Email = d.Email,
                NumTel = d.NumTel
            }).ToList();

            return demandesDTO;
        }

        public async Task<CarteDetails> GetCarteDetailsAsync(string numCarte)
        {
            _logger.LogInformation("Récupération des détails de la carte : {NumCarte}", numCarte);

            // Récupérer la carte depuis le repository
            var carte = await _carteRepository.GetCarteByNumCarteAsync(numCarte);

            if (carte == null)
            {
                _logger.LogWarning("Carte introuvable : {NumCarte}", numCarte);
                throw new InvalidOperationException("Carte introuvable.");
            }

            // Retourner les détails de la carte sous forme de CarteDetails
            return new CarteDetails
            {
                NumCarte = carte.NumCarte, // Numéro de la carte
                NomCarte = carte.NomCarte, // Nom de la carte
                TypeCarte = carte.TypeCarte, // Type de la carte
                DateCreation = carte.DateCreation, // Date de création
                DateExpiration = carte.DateExpiration, // Date d'expiration
                Statut = carte.Statut, // Statut de la carte
                RIB = carte.RIB, // RIB associé
                DateRecuperation = carte.DateRecuperation, // Date de récupération
                // Code CVV
                Iddemande = carte.Iddemande, // Référence à la demande
                PlafondDAP = carte.PlafondDAP, // Plafond DAP
                PlafondTPE = carte.PlafondTPE // Plafond TPE
            };
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            try
            {
                await _emailService.SendEmailAsync(email, subject, message);
                _logger.LogInformation("Email envoyé à {Email} avec le sujet {Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de l'email à {Email}", email);
                throw;
            }
        }

        public async Task<bool> UpdateEmailEnvoyeAsync(int demandeId, bool emailEnvoye)
        {
            return await _carteRepository.UpdateEmailEnvoyeAsync(demandeId, emailEnvoye);
        }

        public async Task<bool> UpdateEmailEnvoyeLivreeAsync(int demandeId, bool emailEnvoyeLivree)
        {
            return await _carteRepository.UpdateEmailEnvoyeLivreeAsync(demandeId, emailEnvoyeLivree);
        }

        public async Task UpdateDemandeAsync(DemandeCarte demande)
        {
            try
            {
                // Vérifier si la demande existe dans la base de données
                var existingDemande = await _dbContext.DemandesCarte
                    .FirstOrDefaultAsync(d => d.Iddemande == demande.Iddemande);

                if (existingDemande == null)
                {
                    throw new InvalidOperationException($"La demande avec l'ID {demande.Iddemande} n'existe pas.");
                }

                // Mettre à jour les propriétés de la demande existante
                _dbContext.Entry(existingDemande).CurrentValues.SetValues(demande);

                // Sauvegarder les modifications dans la base de données
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log l'erreur ou relancer l'exception
                throw new ApplicationException("Une erreur s'est produite lors de la mise à jour de la demande.", ex);
            }
        }

        public async Task<string> BlockCarteAsync(string numCarte)
        {
            // Normaliser le numéro de carte
            numCarte = numCarte.Trim();

            _logger.LogInformation("Recherche de la carte avec le numéro : {NumCarte}", numCarte);

            // Récupérer la carte par son numéro
            var carte = await _carteRepository.GetCarteByNumCarteAsync(numCarte);
            _logger.LogInformation("Carte trouvée : {Carte}", carte);

            if (carte == null)
            {
                throw new InvalidOperationException("Carte introuvable.");
            }

            // Vérifier si la carte est déjà inactive
            if (carte.Statut == STBEverywhere_Back_SharedModels.Models.enums.StatutCarte.Inactive)
            {
                throw new InvalidOperationException("La carte est déjà inactive.");
            }
            // Vérifier si le solde est égal à 0

            // Mettre à jour le statut de la carte à "Inactif"
            carte.Statut = STBEverywhere_Back_SharedModels.Models.enums.StatutCarte.Inactive;
            await _carteRepository.UpdateCarteAsync(carte);
            return "Carte bloquée avec succès.";

        }


        public async Task<string> DeBlockCarteAsync(string numCarte)
        {
            // Normaliser le numéro de carte
            numCarte = numCarte.Trim();

            _logger.LogInformation("Recherche de la carte avec le numéro : {NumCarte}", numCarte);

            // Récupérer la carte par son numéro
            var carte = await _carteRepository.GetCarteByNumCarteAsync(numCarte);
            _logger.LogInformation("Carte trouvée : {Carte}", carte);

            if (carte == null)
            {
                throw new InvalidOperationException("Carte introuvable.");
            }

            // Vérifier si la carte est déjà active
            if (carte.Statut == STBEverywhere_Back_SharedModels.Models.enums.StatutCarte.Active)
            {
                throw new InvalidOperationException("La carte est déjà active.");
            }


            else
            {
                // Mettre à jour le statut de la carte à "Inactif"
                carte.Statut = STBEverywhere_Back_SharedModels.Models.enums.StatutCarte.Active;
                await _carteRepository.UpdateCarteAsync(carte);
                return "Carte debloquée avec succès.";
            }

        }


        public async Task<IEnumerable<DemandeAugmentationPlafond>> GetDemandesPlafondByAgenceIdAsync(string agenceId)
        {
            // Récupérer tous les comptes associés à l'agence
            var comptesAgence = await _dbContext.Comptes
                .Include(c => c.Client)
                .Where(c => c.Client.AgenceId == agenceId)
                .ToListAsync();

            // Récupérer les numéros de carte associés à ces comptes
            var ribComptes = comptesAgence.Select(c => c.RIB).ToList();

            // Récupérer les demandes d'augmentation pour ces cartes
            var demandes = await _dbContext.DemandesAugmentationPlafond
                .Include(d => d.Carte)
                .Where(d => ribComptes.Contains(d.Carte.RIB))
                .OrderByDescending(d => d.DateDemande)
                .ToListAsync();

            return demandes;
        }

        public async Task<bool> RepondreDemandeAugmentationAsync(int demandeId, string nouveauStatut, string commentaire)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                // Récupérer la demande
                var demande = await _dbContext.DemandesAugmentationPlafond
                    .Include(d => d.Carte)
                    .FirstOrDefaultAsync(d => d.Id == demandeId);

                if (demande == null)
                {
                    _logger.LogWarning("Demande d'augmentation introuvable : {DemandeId}", demandeId);
                    return false;
                }

                // Valider la transition de statut
                if (demande.Statut != StatutDemandeAug.EnAttente.ToString() &&
                    nouveauStatut != StatutDemandeAug.Approuvee.ToString() &&
                    nouveauStatut != StatutDemandeAug.Rejetee.ToString())
                {
                    _logger.LogWarning("Transition de statut invalide : {AncienStatut} -> {NouveauStatut}",
                        demande.Statut, nouveauStatut);
                    return false;
                }

                // Mettre à jour la demande
                demande.Statut = nouveauStatut;
                demande.Commentaire = commentaire;
                demande.DateTraitement = DateTime.Now;

                // Si approuvée, mettre à jour les plafonds de la carte
                if (nouveauStatut == StatutDemandeAug.Approuvee.ToString())
                {
                    demande.Carte.PlafondTPE = demande.NouveauPlafondTPE;
                    demande.Carte.PlafondDAP = demande.NouveauPlafondDAB;
                    _dbContext.Cartes.Update(demande.Carte);
                }

                _dbContext.DemandesAugmentationPlafond.Update(demande);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors du traitement de la demande d'augmentation");
                return false;
            }
        }

    }
}
