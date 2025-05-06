using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class HistoriqueRechargeDto
    {
        public int Id { get; set; }
        public DateTime DateRecharge { get; set; }
        public string CarteEmetteurNum { get; set; }
        
        public string CarteRecepteurNum { get; set; }
    
        public decimal Montant { get; set; }
        public decimal Frais { get; set; }
        public bool isDebit { get; set; }


    }
}
