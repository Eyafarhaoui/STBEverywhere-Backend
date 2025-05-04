using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIChequier.Repository.IRepositoy
{
    public interface IFraisChequierRepository
    {
        Task AddAsync(FraisChequier frais);
        Task<IEnumerable<FraisChequier>> GetAllAsync();
        Task<IEnumerable<FraisChequier>> GetByDemandeIdAsync(int idDemande);
        Task SaveAsync();
        //retourne frais chequier du compte
        Task<IEnumerable<FraisChequier>> GetFraisChequiersByCompteAsync(string rib);

    }
}
