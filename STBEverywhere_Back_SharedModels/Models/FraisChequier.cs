using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class FraisChequier
    {


        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string type { get; set; } // frais chequier/frais envoi recommende 

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,3)")]
        public decimal Montant { get; set; }


        [Required]
        public int IdDemande { get; set; }

        [ForeignKey("IdDemande")]
        public DemandeChequier DemandeChequier { get; set; }


        
    }
}
