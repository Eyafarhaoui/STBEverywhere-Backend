using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIChequier.Repository.IRepositoy;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIChequier.Repository
{
    public class DemandesChequiersRepository : IDemandesChequiersRepository
    {

        private readonly ApplicationDbContext _context;

        public DemandesChequiersRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<List<string>> GetRibComptesByClientId(int clientId)
        {
            return await _context.Comptes
                .Where(c => c.ClientId == clientId)
                .Select(c => c.RIB)
                .ToListAsync();
        }



        public async Task<DemandeChequier> GetDemandeByChequier(int demandeId)
        {
            return await _context.DemandesChequiers
                .Include(d => d.Feuilles) 
                .FirstOrDefaultAsync(d => d.IdDemande == demandeId);
        }

        public async Task<IEnumerable<FeuilleChequier>> GetFeuillesByDemandeId(int demandeId)
        {
            return await _context.FeuillesChequiers
                .Where(f => f.DemandeChequierId == demandeId)
                .ToListAsync();
        }

        public async Task<bool> HasDemandeEncours(string ribCompte)
        {
            return await _context.DemandesChequiers
                .AnyAsync(d => d.RibCompte == ribCompte && d.Status ==0);
        }

        public async Task<List<DemandeChequier>> GetDemandesByRibComptes(List<string> ribComptes)
        {
            return await _context.DemandesChequiers
                .Where(d => ribComptes.Contains(d.RibCompte))
                .ToListAsync();
        }

        public async Task<IEnumerable<DemandeChequier>> GetAllAsync()
        {
            return await _context.DemandesChequiers.ToListAsync();
        }

        public async Task<DemandeChequier?> GetByIdAsync(int id)
        {
            return await _context.DemandesChequiers
                .Include(d => d.Feuilles) // Si les feuilles doivent être incluses
                .FirstOrDefaultAsync(d => d.IdDemande == id);
        }
        
        public async Task AddAsync(DemandeChequier demande)
        {
            await _context.DemandesChequiers.AddAsync(demande);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(DemandeChequier demande)
        {
            _context.DemandesChequiers.Update(demande);
            await _context.SaveChangesAsync();
        }
        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<int> CountFeuillesByRib(string ribCompte)
        {
            return await _context.FeuillesChequiers
                .Where(f => f.DemandeChequier.RibCompte == ribCompte)
                .CountAsync();
        }
        public async Task<bool> HasActiveChequier(string ribCompte)
        {
            var chequier = await _context.Chequiers
                .Where(c => c.DemandeChequier.RibCompte == ribCompte && c.Status == ChequierStatus.Actif)
                .FirstOrDefaultAsync();

            return chequier != null; // Si un chéquier actif est trouvé, retourne true
        }
        public async Task<List<DemandeChequier>> GetDemandesByRibCompte(string ribCompte)
        {
            try
            {
                // Récupérer les demandes de chéquiers où le RIB du compte correspond à l'argument
                var demandes = await _context.DemandesChequiers
                                              .Where(d => d.RibCompte == ribCompte)
                                              .ToListAsync();

                return demandes;
            }
            catch (Exception ex)
            {
                // Gestion des erreurs, vous pouvez loguer ou retourner l'erreur
                throw new Exception($"Erreur lors de la récupération des demandes par RIB: {ex.Message}");
            }
        }

        public async Task<bool> IsCompteEpargne(string ribCompte)
        {
            var compte = await _context.Comptes
                .Where(c => c.RIB == ribCompte)
                .FirstOrDefaultAsync();

            if (compte != null && compte.Type == "epargne")
            {
                return true;  // Compte de type épargne
            }

            return false;  // Autre type de compte
        }

        public async Task DeleteAsync(int id)
        {
            var demande = await _context.DemandesChequiers.FindAsync(id);
            if (demande != null)
            {
                _context.DemandesChequiers.Remove(demande);
                await _context.SaveChangesAsync();
            }
        }
    }
}

