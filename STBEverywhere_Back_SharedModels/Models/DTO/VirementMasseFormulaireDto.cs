namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class VirementMasseFormulaireDto
    {


        public string RibEmetteur { get; set; }
    public string Motif { get; set; }
    public string? Description { get; set; }
    public List<BeneficiaireVirementMasseFormulaireDto> Beneficiaires { get; set; }
        public string? Otp { get; set; }
    }
}
