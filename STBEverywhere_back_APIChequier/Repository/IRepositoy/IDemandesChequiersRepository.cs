using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIChequier.Repository.IRepositoy
{
    public interface IDemandesChequiersRepository
    {
        Task<IEnumerable<DemandeChequier>> GetAllAsync();
        Task<DemandeChequier?> GetByIdAsync(int id);
        Task AddAsync(DemandeChequier demande);
        Task UpdateAsync(DemandeChequier demande);
        Task DeleteAsync(int id);
        Task SaveAsync();
        Task<List<DemandeChequier>> GetDemandesByRibComptes(List<string> ribComptes);
        Task<List<DemandeChequier>> GetDemandesByRibCompte(string ribCompte);
        Task<bool> HasDemandeEncours(string ribCompte);
        Task<List<string>> GetRibComptesByClientId(int clientId);
        Task<int> CountFeuillesByRib(string ribCompte);
        Task<bool> IsCompteEpargne(string ribCompte);
        Task<bool> HasActiveChequier(string ribCompte);

        Task<DemandeChequier> GetDemandeByChequier(int idDemande);
        Task<IEnumerable<FeuilleChequier>> GetFeuillesByDemandeId(int demandeId);
    }
}
