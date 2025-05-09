using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class NotificationReclamation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClientId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; }

        [Required]
        [MaxLength(500)]
        public string Message { get; set; }

        public bool IsRead { get; set; } = false;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(50)]
        public string NotificationType { get; set; }

        public int? RelatedId { get; set; }

        [ForeignKey("ClientId")] 
        [JsonIgnore]
        public virtual Client Client { get; set; }
    }
}