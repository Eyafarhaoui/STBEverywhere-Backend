using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APICompte.Services.IServices;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APICompte.Services
{
    public class HistoriqueSoldeService : IHistoriqueSoldeService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<HistoriqueSoldeService> _logger;

        public HistoriqueSoldeService(ApplicationDbContext dbContext, ILogger<HistoriqueSoldeService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task AlimenterHistoriqueSolde()
        {
            _logger.LogInformation("Début de l'alimentation de l'historique des soldes.");

            var comptes = await _dbContext.Comptes.ToListAsync();

            foreach (var compte in comptes)
            {
                var historiqueSolde = new HistoriqueSolde
                {
                    RIB = compte.RIB,
                    Solde = compte.Solde,
                    date_jour = DateTime.UtcNow 
                };

                await _dbContext.HistoriquesSoldes.AddAsync(historiqueSolde);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Alimentation de l'historique terminée.");
        }
    }
}
