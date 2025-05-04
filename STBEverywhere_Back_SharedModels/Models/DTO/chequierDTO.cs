using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class chequierDTO
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ChequierStatus Status { get; set; }

        public DateTime DateLivraison { get; set; }
        public decimal PlafondChequier { get; set; }
        public string RibCompte { get; set; }
        public string Type { get; set; }
    }
}
