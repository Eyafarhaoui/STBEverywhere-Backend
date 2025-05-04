using System.ComponentModel.DataAnnotations;

namespace STBEverywhere_Back_SharedModels.Models
{

    public class Convention
    {
        [Key]
        public int id_convention { get; set; }

        [Required]
        public string nom_convention { get; set; }

        [Required]
        public decimal marge_bancaire { get; set; }  
    }


}



