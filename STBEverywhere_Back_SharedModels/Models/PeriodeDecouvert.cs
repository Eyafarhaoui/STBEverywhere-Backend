using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class PeriodeDecouvert
    {



        [Key]
        public int Id { get; set; }

        [Required]
        public string RIB { get; set; }

        [ForeignKey("RIB")]
        public Compte Compte { get; set; }

        public DateTime DateDebut { get; set; }
        public DateTime? DateFin { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal MontantMaxDecouvert { get; set; }

       
    }
}
