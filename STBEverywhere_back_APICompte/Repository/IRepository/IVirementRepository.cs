
using STBEverywhere_Back_SharedModels;
using System.Linq.Expressions;
namespace STBEverywhere_back_APICompte.Repository.IRepository
{
    public interface IVirementRepository:IRepository<Virement>
    {
        Task<List<Virement>> GetAllAsync(Expression<Func<Virement, bool>> filter = null);
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
       
        Task RollbackTransactionAsync();
        Task UpdateAsync(Virement entity);
        Task EnregistrerVirements(List<Virement> virements);
    }
}
