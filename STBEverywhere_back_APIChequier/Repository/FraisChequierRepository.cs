using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIChequier.Repository.IRepositoy;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIChequier.Repository
{
    public class FraisChequierRepository : IFraisChequierRepository
    {
        private readonly ApplicationDbContext _context;

        public FraisChequierRepository(ApplicationDbContext context)
        {
            _context = context;
        }


        public async Task<IEnumerable<FraisChequier>> GetFraisChequiersByCompteAsync(string rib)
        {
            return await _context.FraisChequiers
                .Where(f => _context.DemandesChequiers
                    .Any(d => d.IdDemande == f.IdDemande && d.RibCompte == rib))
                .ToListAsync();
        }

        public async Task AddAsync(FraisChequier frais)
        {
            await _context.FraisChequiers.AddAsync(frais);
        }

        public async Task<IEnumerable<FraisChequier>> GetAllAsync()
        {
            return await _context.FraisChequiers.ToListAsync();
        }

        public async Task<IEnumerable<FraisChequier>> GetByDemandeIdAsync(int idDemande)
        {
            return await _context.FraisChequiers
                .Where(f =>     f.IdDemande == idDemande)
                .ToListAsync();
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }

}
