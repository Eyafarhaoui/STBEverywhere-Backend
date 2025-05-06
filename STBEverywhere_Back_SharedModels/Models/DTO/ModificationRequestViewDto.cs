namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class ModificationRequestViewDto
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public string FieldToModify { get; set; }
        public string NewValue { get; set; }
        public string JustificationPath { get; set; }
        public DateTime RequestDate { get; set; }
        public string Status { get; set; }
        public int? ProcessedByAgentId { get; set; }
        public string? ProcessedByAgentName { get; set; }
        public DateTime? ProcessedDate { get; set; }
        public string? RejectionReason { get; set; }
    }
}
