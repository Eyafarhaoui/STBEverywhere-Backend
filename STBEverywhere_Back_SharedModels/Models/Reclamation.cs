using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class Reclamation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        
        public int ClientId { get; set; }

        [ForeignKey("ClientId")]
        [JsonIgnore]
        public Client Client { get; set; }

        [Required]
        [MaxLength(100)]
        public string Objet { get; set; }

        [Required]
        public string Motif { get; set; }

        
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; }

        [Required]
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        public string? Reponse { get; set; }

        public int? IdAgent { get; set; }

        public DateTime? DateResolution { get; set; }

        [Required]
        public ReclamationStatut Statut { get; set; } = ReclamationStatut.EnCours;
    }
    public enum ReclamationStatut
    {
        EnCours,
        traite 
    }
}
