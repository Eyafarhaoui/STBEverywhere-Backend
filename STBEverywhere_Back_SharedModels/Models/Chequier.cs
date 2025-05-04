using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace STBEverywhere_Back_SharedModels.Models
{
    public class Chequier
    {

        [Key]
        public int Id { get; set; }

        [Required]
        public int DemandeChequierId { get; set; }

        [ForeignKey("DemandeChequierId")]
        public DemandeChequier DemandeChequier { get; set; }

        [Required]
        public ChequierStatus Status { get; set; } = ChequierStatus.Actif;


        public DateTime? DateLivraison { get; set; }

        public int? IdAgent { get; set; }
        public ICollection<FraisChequier> FraisComptes { get; set; } = new List<FraisChequier>();

        //public List<FeuilleChequier> Feuilles { get; set; } = new List<FeuilleChequier>();

    }

    public enum ChequierStatus
    {
        Actif,// Le chéquier est actif et peut être utilisé
        EnOpposition, // client met chequier en opposition en cas de  (perte,vol,fraude.)
        Bloqué_agent,
        epuisé
    }

}
