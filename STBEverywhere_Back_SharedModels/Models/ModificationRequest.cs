using STBEverywhere_Back_SharedModels;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

public class ModificationRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    [ForeignKey("ClientId")]
    public virtual Client Client { get; set; }

    [Required]
    [Column(TypeName = "varchar(50)")]
    public string FieldToModify { get; set; }

    [Required]
    [Column(TypeName = "varchar(255)")]
    public string NewValue { get; set; }

    [Required]
    [Column(TypeName = "varchar(255)")]
    public string JustificationPath { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "varchar(20)")]
    public string Status { get; set; } = "Pending";

    public int? ProcessedByAgentId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ProcessedDate { get; set; }
}