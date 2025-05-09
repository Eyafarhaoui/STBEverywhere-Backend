using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels.Models.enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class Carte
    {
        [Key]
        [Required]
        public string NumCarte { get; set; } // Numéro de la carte (identifiant unique)

        [Required]
        public NomCarte NomCarte { get; set; } // Utilisation de l'enum pour le nom de la carte

        [Required]
        public TypeCarte TypeCarte { get; set; }  // Utilisation de l'enum pour le type de carte

        [Required]
        public DateTime DateCreation { get; set; } // Date de création de la carte

        [Required]
        public DateTime DateExpiration { get; set; } // Date d'expiration de la carte

        [Required]
        public StatutCarte Statut { get; set; }  // Utilisation de l'enum pour le statut de la carte

        public string? Nature { get; set; }  // postpayee, prepayee

        [Required]
        public int Iddemande { get; set; } // Référence à la demande de carte

        [ForeignKey("Iddemande")]
        [JsonIgnore]
        public DemandeCarte DemandeCarte { get; set; }

        // Clé étrangère vers Compte
        [Required]
        public string RIB { get; set; }
        public decimal Solde => Compte?.Solde ?? 0;

        [ForeignKey("RIB")]
        public Compte Compte { get; set; } // Relation avec Compte

        // Nouveaux champs
        public DateTime? DateRecuperation { get; set; } // Date de récupération de la carte (nullable)

        [Required]
        public string CodePIN { get; set; } // Code PIN à 4 chiffres

       

        [Column(TypeName = "decimal(18, 2)")]
        public decimal PlafondTPE { get; set; } // Par défaut 4000 pour toutes les cartes

        public decimal PlafondDAP { get; set; } // Par défaut 2000 pour toutes les cartes

        [JsonIgnore]
        public ICollection<FraisCarte> FraisCartes { get; set; } = new List<FraisCarte>();
        [JsonIgnore]
        public ICollection<DemandeAugmentationPlafond> DemandesAugmentation { get; set; } = new List<DemandeAugmentationPlafond>();
    }
}