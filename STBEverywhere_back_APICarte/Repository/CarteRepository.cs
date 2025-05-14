using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using STBEverywhere_Back_SharedModels.Models.enums;
using STBEverywhere_Back_SharedModels;

namespace STBEverywhere_back_APICarte.Repository
{
    public class CarteRepository : ICarteRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CarteRepository> _logger;

        public CarteRepository(ApplicationDbContext context, ILogger<CarteRepository> logger)
        {
            _context = context;
            _logger = logger;
        }
        public async Task<bool> AddFraisToCarteAsync(string numCarte, FraisCarte frais)
        {
            try
            {
                _context.FraisCartes.Add(frais);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<Carte?> GetCarteActiveByRIBAndNomAndTypeAsync(string rib, NomCarte nomCarte, TypeCarte typeCarte)
        {
            return await _context.Cartes
                .FirstOrDefaultAsync(c =>
                    c.RIB == rib &&
                    c.NomCarte == nomCarte &&
                    c.TypeCarte == typeCarte &&
                    c.Statut == StatutCarte.Active // Assurez-vous que ce statut existe dans votre modèle
                );
        }
        public async Task<bool> UpdateCarteAsync(Carte carte)
        {
            _context.Cartes.Update(carte);
            return await _context.SaveChangesAsync() > 0;
        }

       
        public async Task<IEnumerable<Carte>> GetCartesByRIBAsync(string rib)
        {
            return await _context.Cartes
                .Where(c => c.RIB == rib)
                .ToListAsync();
        }

        public async Task<bool> CreateDemandeCarteAsync(DemandeCarte demandeCarte)
        {
            await _context.DemandesCarte.AddAsync(demandeCarte);
            return await SaveChangesAsync();
        }

        public async Task<IEnumerable<DemandeCarte>> GetDemandesByStatutAsync(StatutDemande statut)
        {
            _logger.LogInformation("Exécution de la requête pour récupérer les demandes avec le statut : {Statut}", statut);

            var demandes = await _context.DemandesCarte
                .Where(d => d.Statut == statut)
                .ToListAsync();

            _logger.LogInformation("Nombre de demandes trouvées : {Count}", demandes.Count);

            return demandes;
        }

        public async Task<DemandeCarte> GetDemandeCarteByIdAsync(int demandeId)
        {
            return await _context.DemandesCarte
                .FirstOrDefaultAsync(d => d.Iddemande == demandeId);
        }

        public async Task<bool> CreateCarteAsync(Carte carte)
        {
            await _context.Cartes.AddAsync(carte);
            return await SaveChangesAsync();
        }
        public async Task<bool> PinExistsAsync(String pin)
        {
            return await _context.Cartes.AnyAsync(c => c.CodePIN == pin);
        }

       

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
        public async Task<IEnumerable<DemandeCarte>> GetDemandesByRIBAsync(string rib)
        {
            return await _context.DemandesCarte
                .Where(d => d.NumCompte == rib)
                .ToListAsync();
        }

        public async Task<IEnumerable<DemandeCarte>> GetDemandesByCompteAndNomAndTypeAsync(string numCompte,NomCarte nomCarte,TypeCarte typeCarte)
        {
            return await _context.DemandesCarte
                .Where(d => d.NumCompte == numCompte && d.NomCarte == nomCarte && d.TypeCarte == typeCarte)
                .ToListAsync();
        }
        public async Task<bool> UpdateEmailEnvoyeAsync(int demandeId, bool emailEnvoye)
        {
            var demande = await _context.DemandesCarte.FindAsync(demandeId);
            if (demande == null)
            {
                return false;
            }

            demande.EmailEnvoye = emailEnvoye;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> UpdateEmailEnvoyeLivreeAsync(int demandeId, bool emailEnvoyeLivree)
        {
            var demande = await _context.DemandesCarte.FindAsync(demandeId);
            if (demande == null)
            {
                return false;
            }

            demande.EmailEnvoyeLivree = emailEnvoyeLivree;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CardNumberExistsAsync(string cardNumber)
        {
            return await _context.Cartes.AnyAsync(c => c.NumCarte == cardNumber);
        }
        public async Task<Carte> GetCarteByNumCarteAsync(string numCarte)
        {
            return await _context.Cartes.FirstOrDefaultAsync(c => c.NumCarte == numCarte);
        }





        public async Task<IEnumerable<FraisCarte>> GetFraisByNumCarteAsync(string numCarte)
        {
            return await _context.FraisCartes
                                 .Where(f => f.NumCarte == numCarte)
                                 .ToListAsync();
        }






    }
}