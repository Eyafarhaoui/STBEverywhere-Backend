namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class ModificationRequestDto
    {
        public string FieldToModify { get; set; }
        public string NewValue { get; set; }
        public IFormFile JustificationFile { get; set; }
    }
}
