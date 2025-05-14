namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class VirementUnitaireDto
    {
        public string RIB_Emetteur { get; set; }   // RIB du compte émetteur (obligatoire)
        public string? RIB_Recepteur { get; set; }   // avec les virements vers mes comptes
        public decimal Montant { get; set; }       // Montant à virer (obligatoire)
        public string Description { get; set; }

        public string motif { get; set; }
        public string TypeVirement { get; set; }

        public int? IdBeneficiaire { get; set; } // avec les virements vers d'autres comptes

        public string? OtpCode { get; set; } // Pour validation
        public int Id { get; set; }
    }
}

