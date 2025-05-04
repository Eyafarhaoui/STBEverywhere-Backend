namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class FraisCarteDto
    {
        public int Id { get; set; }
        public string Type { get; set; }          
        public DateTime Date { get; set; }
        public double Montant { get; set; }
        public string NumCarte { get; set; }
    }

}
