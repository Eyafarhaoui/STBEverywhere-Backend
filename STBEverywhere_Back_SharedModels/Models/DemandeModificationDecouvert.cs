using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class DemandeModificationDecouvert
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RIBCompte { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,3)")]
         public decimal DecouvertDemande { get; set; }

        [Required]
        public StatutDemandeEnum StatutDemande { get; set; } = StatutDemandeEnum.EnAttente;

        [Required]
        public DateTime DateDemande { get; set; } = DateTime.Now;

        public string? MotifRefus { get; set; }

        public int? IdAgentRepondant { get; set; }
        public DateTime? DateTraitement { get; set; }
        public bool NotificationEnvoyee { get; set; } = false;

        public bool MailEnvoyee { get; set; } = false;

        [ForeignKey("RIBCompte")]
        public Compte Compte { get; set; }
    }


    public enum StatutDemandeEnum
    { 
        EnAttente = 0,
        Accepte = 1,
        Refuse = 2
    }
}



