using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APICompte.Repository.IRepository;
using STBEverywhere_back_APICompte.Services.IServices;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APICompte.Services
{
    public class CalculInteretsService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CalculInteretsService> _logger;
        private readonly ICompteService _compteService;
        private readonly IVirementRepository _dbVirement;
        private const decimal TauxInteretAnnuel = 0.03m;

        public CalculInteretsService(IVirementRepository dbVirement,ICompteService compteService,ApplicationDbContext dbContext, ILogger<CalculInteretsService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _compteService = compteService;
            _dbVirement = dbVirement;
        }

        public async Task CalculerInteretsQuotidiens()
        {
            _logger.LogInformation("Début du calcul des intérêts quotidiens...");

            var comptes = await _compteService.GetAllAsync(c => c.Type == "Epargne");
            foreach (var compte in comptes)
            {
                // Calcul des intérêts du jour
                decimal interetJour = compte.Solde * (TauxInteretAnnuel / 365);

                var interetJournalier = new InteretJournalier
                {
                    RIB = compte.RIB,
                    Montant = interetJour,
                    DateCalcul = DateTime.UtcNow.Date
                };

                await _dbContext.InteretsJournaliers.AddAsync(interetJournalier);

                var historiqueSolde = new HistoriqueSolde
                {
                    RIB = compte.RIB,
                    Solde = compte.Solde,
                    date_jour = DateTime.UtcNow.Date
                };
                await _dbContext.HistoriquesSoldes.AddAsync(historiqueSolde);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Calcul des intérêts quotidiens terminé.");
        }




        public async Task VerserInteretsTrimestriels()
        {
            _logger.LogInformation("Début du versement des intérêts trimestriels");

            var comptes = await _compteService.GetAllAsync(c => c.Type == "Epargne");
            foreach (var compte in comptes)
            {
                //  intérêts 3 derniers mois 
                var dateDebutTrimestre = DateTime.UtcNow.AddMonths(-3).Date;

                var interetsTrimestre = await _dbContext.InteretsJournaliers
                    .Where(i => i.RIB == compte.RIB && i.DateCalcul >= dateDebutTrimestre)
                    .ToListAsync();

                decimal totalInterets = interetsTrimestre.Sum(i => i.Montant);

                // prime de fidélité
                decimal primeFidelite = CalculerPrimeFidelite(compte);

                decimal montantVerse = totalInterets + primeFidelite;

                
                compte.Solde += montantVerse;


                _logger.LogInformation($"Versement de {montantVerse} TND sur le compte {compte.RIB}");
                int mois = DateTime.Now.Month;
                string trimestre = mois switch
                {
                    <= 3 => "1ER TRIMESTRE",
                    <= 6 => "2ÈME TRIMESTRE",
                    <= 9 => "3ÈME TRIMESTRE",
                    _ => "4ÈME TRIMESTRE"
                };
                var virement = new Virement
                {
                    RIB_Emetteur = "STB_INTERETS",
                    RIB_Recepteur = compte.RIB,
                    Montant = montantVerse,
                    DateVirement = DateTime.Now,
                    StatutVirement = "Réussi",
                    Motif = $"INT. EPARGNE {trimestre} {DateTime.Now.Year}",
                    TypeVirement = "virement intérêt ",
                    //Description = virementDto.Description
                };

                await _dbVirement.CreateAsync(virement);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Versement des intérêts trimestriels terminé.");
        }












        private decimal CalculerPrimeFidelite(Compte compte)
        {
            var dateIlYA1An = DateTime.UtcNow.AddYears(-1);
            var dateIlYA2Ans = DateTime.UtcNow.AddYears(-2);

            // On regarde le plus ancien solde dans HistoriqueSolde
            var historique = _dbContext.HistoriquesSoldes
                .Where(h => h.RIB == compte.RIB && h.date_jour <= dateIlYA1An)
                .OrderBy(h => h.date_jour)
                .FirstOrDefault();

            if (historique == null)
                return 0; // Pas de prime

            // Si solde constant depuis 2 ans  prime 1%
            if (historique.date_jour <= dateIlYA2Ans)
            {
                return compte.Solde * 0.01m;
            }
            // Sinon prime 0,5%
            return compte.Solde * 0.005m;
        }

















    }



}
