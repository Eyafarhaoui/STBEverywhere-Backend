using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace STBEverywhere_Back_SharedModels.Models.DTO
{
    public class UploadFichierRequest
    {
        [Required]
        public IFormFile? Fichier { get; set; }
        [FromForm]
        public string? OtpSaisi { get; set; }
    }
}
