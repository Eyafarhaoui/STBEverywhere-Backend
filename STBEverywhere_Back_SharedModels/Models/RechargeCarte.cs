

    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models
{
        [Table("RechargesCarte")] // Nom de la table dans la base de données
 
    public class RechargeCarte
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(16)]
        [ForeignKey("CarteEmetteur")]
        public string CarteEmetteurNum { get; set; }

        [Required]
        [StringLength(16)]
        [ForeignKey("CarteRecepteur")]
        public string CarteRecepteurNum { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 3)")]
        [Range(0.01, 100000)]
        public decimal Montant { get; set; }

        [Required]
        public DateTime DateRecharge { get; set; } = DateTime.UtcNow;

        // Supprimez le champ Frais et ajoutez la relation
        [JsonIgnore]
        public virtual ICollection<FraisCarte> Frais { get; set; } = new List<FraisCarte>();

        [JsonIgnore]
        public virtual Carte CarteEmetteur { get; set; }

        [JsonIgnore]
        public virtual Carte CarteRecepteur { get; set; }
    }
}

