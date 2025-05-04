using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using STBEverywhere_Back_SharedModels.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels
{
    public class Client
    {
        [Key]
        public int Id { get; set; }  // Clé primaire

        [Required]
        public string Nom { get; set; }

        [Required]
        public string Prenom { get; set; }

        public DateTime DateNaissance { get; set; }

        [Required]
        public string Telephone { get; set; }

        [Required]
        public string Email { get; set; }

        [Required]
        public string Adresse { get; set; }

        public string Civilite { get; set; }
        public string Nationalite { get; set; }
        public string EtatCivil { get; set; }
        public string Residence { get; set; }
        public string? NumCIN { get; set; }
        public DateTime? DateDelivranceCIN { get; set; }
        public DateTime? DateExpirationCIN { get; set; }
        public string? LieuDelivranceCIN { get; set; }
        public string? PhotoClient { get; set; }
  
        public string? ResetPasswordToken { get; set; }
        public string Genre { get; set; } // Nouveau champ : Genre (Masculin, Féminin, Autre)
        public string Profession { get; set; } // Nouveau champ : Profession
        public string SituationProfessionnelle { get; set; } // Nouveau champ : Situation professionnelle
        public string NiveauEducation { get; set; } // Nouveau champ : Niveau d'éducation
        public int NombreEnfants { get; set; } // Nouveau champ : Nombre d'enfants
        public decimal RevenuMensuel { get; set; }

        public string? PaysNaissance { get; set; }
        public string? NomMere { get; set; }
        public string?NomPere { get; set; }


        [Required]
        [StringLength(24)] // Longueur fixe pour ObjectId MongoDB
        public string AgenceId { get; set; }

        


        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
        public DateTime? ResetPasswordTokenExpiry { get; set; }



        public int? id_convention { get; set; }

        [ForeignKey("id_convention")]
        public  Convention Convention { get; set; }



        [JsonIgnore]
        public ICollection<Compte> Comptes { get; set; } = new List<Compte>();
        [JsonIgnore]
        public virtual ICollection<Reclamation> Reclamations { get; set; } = new List<Reclamation>();
        [JsonIgnore]
        public ICollection<Beneficiaire> Beneficiaires { get; set; } = new List<Beneficiaire>();

        [JsonIgnore]
        public virtual ICollection<NotificationPack> NotificationsPack { get; set; } = new List<NotificationPack>();

        [JsonIgnore]
        public virtual ICollection<NotificationReclamation> NotificationsReclamation { get; set; } = new List<NotificationReclamation>();
    }
}
