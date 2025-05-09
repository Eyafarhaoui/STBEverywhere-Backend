using System.ComponentModel.DataAnnotations;

namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class RechargeCarteDto
    {
        [Required]
        [StringLength(16, MinimumLength = 16)]
        public string CarteEmetteurNum { get; set; }

        [Required]
        [StringLength(16, MinimumLength = 16)]
        public string CarteRecepteurNum { get; set; }

        [Required]
        [Range(0.01, 100000)]
        public decimal Montant { get; set; }

        public string Cvv { get; set; }
        public string DateExpiration { get; set; } // Format "MM/yy"
    }
}
