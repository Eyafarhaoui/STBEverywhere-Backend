namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class ProcessRequestDto
    {
        public bool Approve { get; set; }
        public string? RejectionReason { get; set; } // Optionnel: motif de rejet
    }
}
