using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIChequier.Repository.IRepositoy
{
    public interface IChequierRepository
    {
        Task<List<DemandeChequier>> GetChequiersDisponiblesAsync();
        Task<List<DemandeChequier>> GetChequiersExpedieAsync();
        Task<List<DemandeChequier>> GetDemandesByRibComptes(List<string> ribComptes);
        Task<List<Chequier>> GetChequiersByDemandesIds(List<int> demandesIds);
        Task<Chequier> GetByIdAsync(int chequierId);
        Task<Chequier?> GetByDemandeIdAsync(int demandeId);
        Task AddAsync(Chequier chequier);
        Task SaveAsync();

        Task SaveChangesAsync();
    }
}
