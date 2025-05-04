using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class FraisCompte
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string type { get; set; } // "VirementMultiple", "VirementUnitaire".

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,3)")]
        public decimal Montant { get; set; }

        // Stockage des IDs sous forme "1,2,3"
        public string IdsVirementsStr { get; set; } = "";

        // Propriété pratique pour travailler avec une liste
        [NotMapped]
        public List<int> IdsVirements
        {
            get => string.IsNullOrEmpty(IdsVirementsStr)
                ? new List<int>()
                : IdsVirementsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(int.Parse)
                                .ToList();
            set => IdsVirementsStr = value != null ? string.Join(",", value) : "";
        }

        [Required]
        public string RIB { get; set; }

        [ForeignKey("RIB")]
        public Compte Compte { get; set; }
    }
}
