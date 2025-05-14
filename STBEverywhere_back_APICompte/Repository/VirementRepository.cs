using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_back_APICompte.Repository.IRepository;
using STBEverywhere_Back_SharedModels;
using System.Linq.Expressions;
namespace STBEverywhere_back_APICompte.Repository
{
    public class VirementRepository: Repository<Virement>,IVirementRepository
    {
        private readonly ApplicationDbContext _db;
        private IDbContextTransaction _transaction;


        public VirementRepository(ApplicationDbContext db) : base(db) { _db = db; }



        public async Task<List<Virement>> GetAllAsync(Expression<Func<Virement, bool>> filter = null)

        {
            IQueryable<Virement> query = _db.Virements;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            return await query.ToListAsync();
        }


        public async Task<Virement> GetByIdAsync(int id) 
        {
          
            return await _db.Set<Virement>().FindAsync(id);
        }



        /* public async Task CreateAsync(Virement virement)
         {
             await _db.Virement.AddAsync(virement);
             await SaveAsync();
         }*/


        /* public async Task SaveAsync()
         {
             await _db.SaveChangesAsync();
         }*/

        public async Task BeginTransactionAsync()
        {
            _transaction = await _db.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
            }
        }
        public async Task EnregistrerVirements(List<Virement> virements)
        {
            await _db.Virements.AddRangeAsync(virements);
            await _db.SaveChangesAsync();
        }
        public async Task CreateAsync(Virement entity)
        {
            await _db.Virements.AddAsync(entity);
            await _db.SaveChangesAsync();
        }
        public async Task UpdateAsync(Virement entity)
        {

            _db.Virements.Update(entity);
            await
                 _db.SaveChangesAsync();
        }


        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync(); /*nettoyer correctement les ressources de 
                    la transaction qui sontt des ressources asynchrones) afin d'éviter de laisser 
                        des connexions ouvertes ou des transactions non terminées.*/
            }
        }

    }
}
