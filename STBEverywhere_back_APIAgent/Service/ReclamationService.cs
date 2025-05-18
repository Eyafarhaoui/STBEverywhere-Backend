using STBEverywhere_back_APIAgent.Repository.IRepository;
using STBEverywhere_back_APIAgent.Service.IService;

using STBEverywhere_back_APIClient.Services;


using STBEverywhere_back_APIClient.Services;

using STBEverywhere_Back_SharedModels.Models;
using System.Text;
using System.Text.Json;

namespace STBEverywhere_back_APIAgent.Service
{
    public class ReclamationService : IReclamationService
    {
        private readonly IReclamationRepository _reclamationRepository;

        private readonly INotificationService _notificationService;
        private readonly EmailService _emailService;

        public ReclamationService(IReclamationRepository reclamationRepository, INotificationService notificationService, EmailService emailService)
        {
            _reclamationRepository = reclamationRepository;
            _notificationService = notificationService;
            _emailService = emailService;

        }

        public async Task<bool> RepondreAReclamationAsync(int reclamationId, string contenuReponse, int idAgent)
        {
            
            var reclamation = await _reclamationRepository.GetByIdAsync(reclamationId);
            if (reclamation == null || reclamation.Statut == ReclamationStatut.traite)
                return false;

          
            reclamation.Reponse = contenuReponse;
            reclamation.IdAgent = idAgent;
            reclamation.DateResolution = DateTime.UtcNow;
            reclamation.Statut = ReclamationStatut.traite;
            // Envoyer une notification
            await _notificationService.NotifyPackStatusChange(
                reclamation.ClientId,
                "Reclamtion",
                reclamationId,
                "verifier la reponse sur mail ");


            await _reclamationRepository.UpdateAsync(reclamation);

            var email = reclamation.Client?.Email;
            if (string.IsNullOrWhiteSpace(email))
                return false;
            var salutation = reclamation?.Client?.Genre == "Féminin" ? "Madame" : "Monsieur";
            var nomClient = $"{reclamation?.Client?.Prenom} {reclamation?.Client?.Nom}";
var objetReclamation = reclamation.Objet;
            var emailContent = $@"
        <p><strong>{salutation} {nomClient},</strong></p>

        <p>Suite à votre réclamation concernant : <strong>« {objetReclamation} »</strong>, nous vous apportons la réponse suivante :</p>

        <p>{contenuReponse}</p>

        <p>Nous espérons que cette réponse vous apportera satisfaction. Pour toute information complémentaire, n'hésitez pas à contacter votre conseiller.</p>

        <p>Cordialement,<br>
       
        Service Réclamations<br>
        Société Tunisienne de Banque</p>

        <p style='font-size: small; color: gray;'>
        <i>Ce message a été généré automatiquement. Merci de ne pas y répondre.</i>
        </p>";

            //  Préparer l'objet du mail
            var emailRequest = new
            {
                to = email,
                subject = "Réponse à votre réclamation",
                content = emailContent
            };

            //  Appel HTTP POST vers ton propre controller
            using (var httpClient = new HttpClient())
            {
                var json = JsonSerializer.Serialize(emailRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("http://localhost:5203/api/Email/send", content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    
                    return false;
                }
            }


            /*var email = reclamation.Client?.Email;
            if (string.IsNullOrWhiteSpace(email))
                return false;
            await _emailService.SendEmailAsync(email, "Réponse à votre réclamation", contenuReponse);
            return true;*/
        }
    }

}
