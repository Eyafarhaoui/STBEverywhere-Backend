using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIAgent.Repository.IRepository;
using STBEverywhere_back_APIAgent.Service.IService;
using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIAgent.Service
{

    public class DemandeModificationDecouvertService : IDemandeModificationDecouvertService
    {
        private readonly IDemandeModificationDecouvertRepository _repository;

        public DemandeModificationDecouvertService(IDemandeModificationDecouvertRepository repository)
        {
            _repository = repository;
        }

        public async Task<DemandeModificationDecouvert?> GetDemandeByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task RepondreDemandeAsync(DemandeModificationDecouvert demande, bool accepte, string? motifRefus, int idAgent)
        {
            demande.StatutDemande = accepte ? StatutDemandeEnum.Accepte : StatutDemandeEnum.Refuse; ;
            demande.MotifRefus = accepte ? null : motifRefus;
            demande.IdAgentRepondant = idAgent;
            demande.DateTraitement = DateTime.Now;
            await _repository.UpdateAsync(demande);
        }


        public async Task<List<DemandeModificationDecouvert>> GetDemandesParStatutAsync(string statut)
        {
            return await _repository.GetByStatutAsync(statut);
        }


    }
}
