using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class InteretJournalier
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RIB { get; set; }
        [JsonIgnore]
        [ForeignKey("RIB")]

        public Compte Compte { get; set; }

        public decimal Montant { get; set; }

        public DateTime DateCalcul { get; set; }
    }

}
