using SixLabors.ImageSharp.ColorSpaces;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models;
using System.Linq.Expressions;

namespace STBEverywhere_back_APICompte.Services.IServices
{
    public interface ICompteService
    {
        Task<List<Compte>> GetAllAsync(Expression<Func<Compte, bool>> filter = null);
        //Task<Compte> GetByRibAsync(string rib);
        Task<Client> GetClientByRIBAsync(string rib);
        Task<Compte> UpdateAsync(Compte entity);
        Task<Compte> CreateAsync(Compte entity);
        Task SaveAsync();
        string GenerateIBANFromRIB(string rib);
        Task<string> GenerateUniqueRIB(string userId);
        Task<IEnumerable<DemandeModificationDecouvert>> GetDemandesByAgenceIdAsync(string agenceId);


        Task<decimal> GetSoldeByRIBAsync(string rib);
        Task<IEnumerable<DemandeModificationDecouvert>> GetDemandesByClientIdAsync(int clientId);
        Task CreateDemandeModificationAsync(DemandeModificationDecouvert demande);


        //Task<IEnumerable<DemandeModificationDecouvert>> GetDemandesModificationAsync(string ribCompte, string statut);



        Task<IEnumerable<DemandeModificationDecouvert>> GetDemandesModificationAsync(string ribCompte, StatutDemandeEnum statut);

        //Task<Compte> GetByRibAsync(string rib);
        Task<Compte> GetByRIBAsync(string rib);
        Task<string> GetAgenceIdOfCompteAsync(string rib);
        Task<byte[]> GeneratePdfRIBWithQuestPDF(Client client, string rib, IWebHostEnvironment env, string iban, DateTime dateCreation);
        Task<byte[]> GeneratePdfExtraitWithQuestPDF(string rib, DateTime dateDebut, DateTime dateFin, string Statut, string IBAN, decimal solde, IWebHostEnvironment hostingEnvironment);
    }
}