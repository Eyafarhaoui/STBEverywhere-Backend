using System.ComponentModel.DataAnnotations;

namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class ConventionDto
    {
        public int id_convention { get; set; }

        public string nom_convention { get; set; }

        public decimal marge_bancaire { get; set; }
    }
}
