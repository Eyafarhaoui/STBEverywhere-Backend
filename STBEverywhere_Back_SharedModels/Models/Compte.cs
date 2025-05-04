using STBEverywhere_Back_SharedModels.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels
{
    public class Compte
    {

        [Required]
        public string RIB { get; set; }

        [Required]
        public string IBAN { get; set; }
        [Required]
        public string Type { get; set; }

        //[Column(TypeName = "decimal(18,3)")]
        public decimal Solde { get; set; }
        [NotMapped]
        public decimal? SoldeDisponible => Solde + DecouvertAutorise; // Solde total disponible

        public DateTime DateCreation { get; set; }
        public string Statut { get; set; }
        public string NumCin { get; set; }

        public decimal? DecouvertAutorise { get; set; }

        public string? idAgent { get; set; }
        public string NbrOperationsAutoriseesParJour = "illimité";


        public string MontantMaxAutoriseParJour = "illimité";  

        public int ClientId { get; set; } // Clé étrangère
        [JsonIgnore]
        [ForeignKey("ClientId")]
      
        public Client Client { get; set; } // Relation avec Client

        // Relation One-to-Many avec Carte
        public ICollection<Carte> Cartes { get; set; } = new List<Carte>(); // Collection de cartes
        public ICollection<DemandeCarte> DemandesCarte { get; set; } = new List<DemandeCarte>();
        public ICollection<PeriodeDecouvert> PeriodesDecouvert { get; set; }
        public ICollection<FraisCompte> FraisComptes { get; set; } = new List<FraisCompte>();
        public ICollection<HistoriqueSolde> HistoriqueSoldes { get; set; } = new List<HistoriqueSolde>();

    }
}