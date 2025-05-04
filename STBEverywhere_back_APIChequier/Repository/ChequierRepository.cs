using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIChequier.Repository.IRepositoy;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIChequier.Repository
{
    public class ChequierRepository : IChequierRepository
    {
        private readonly ApplicationDbContext _context;

        public ChequierRepository(ApplicationDbContext context)
        {
            _context = context;
        }



        public async Task<Chequier?> GetByDemandeIdAsync(int demandeId)
        {
            return await _context.Chequiers.FirstOrDefaultAsync(c => c.DemandeChequierId == demandeId);
        }

        public async Task AddAsync(Chequier chequier)
        {
            await _context.Chequiers.AddAsync(chequier);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }


        public async Task<Chequier> GetByIdAsync(int chequierId)
        {
            return await _context.Chequiers.FirstOrDefaultAsync(c => c.Id == chequierId);
        }

        public async Task<List<Chequier>> GetChequiersByDemandesIds(List<int> demandesIds)
        {
            return await _context.Chequiers
                .Where(c => demandesIds.Contains(c.DemandeChequierId))
                .ToListAsync();
        }
        public async Task<List<DemandeChequier>> GetDemandesByRibComptes(List<string> ribComptes)
        {
            return await _context.DemandesChequiers
                .Where(d => ribComptes.Contains(d.RibCompte))
                .ToListAsync();
        }
        public async Task<List<DemandeChequier>> GetChequiersDisponiblesAsync()
        {
            return await _context.DemandesChequiers
                .Where(c => c.Status == DemandeStatus.DisponibleEnAgence)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<List<DemandeChequier>> GetChequiersExpedieAsync()
        {
            return await _context.DemandesChequiers
                 .Where(c => c.Status == DemandeStatus.Expedie)
                 .ToListAsync();
        }
    }
}
