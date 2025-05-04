using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class HistoriqueSolde
    {
        [Required]
        [Key]
        public int Id_historique { get; set; }
        public decimal Solde { get; set; }
        public DateTime date_jour { get; set; }
     
        [Required]
        public string RIB { get; set; } 
        [JsonIgnore]
        [ForeignKey("RIB")]

        public Compte Compte { get; set; }

       
    }
}
