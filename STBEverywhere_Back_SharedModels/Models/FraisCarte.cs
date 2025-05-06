using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models
{

    public class FraisCarte
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        [Column(TypeName = "varchar(50)")] // Stockage textuel
        public string Type { get; set; }  // Changé à string au lieu de l'enum

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Montant { get; set; }

        // Clé étrangère vers Carte
        [Required]
        public string NumCarte { get; set; }

        [ForeignKey("NumCarte")]
        [JsonIgnore]
        public Carte Carte { get; set; }
        [Column("IdsRecharges")]
        public string IdsRechargesStr { get; set; } = string.Empty;

        // Propriété pratique pour travailler avec une liste
        [NotMapped]
        public List<int> IdsRecharges
        {
            get => string.IsNullOrEmpty(IdsRechargesStr)
                ? new List<int>()
                : IdsRechargesStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(int.Parse)
                                .ToList();
            set => IdsRechargesStr = value != null ? string.Join(",", value) : "";
        }
    }

    public enum TypeFraisCarte
    {
        [Display(Name = "Création")]
        Creation = 0,

        [Display(Name = "Renouvellement")]
        Renouvellement = 1,

        [Display(Name = "Retrait ATM")]
        Retrait = 2,

        [Display(Name = "Conversion devise")]
        Conversion = 3,

        [Display(Name = "Pénalité")]
        Penalite = 4
    }
}
