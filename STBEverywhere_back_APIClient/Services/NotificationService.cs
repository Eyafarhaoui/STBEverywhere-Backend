using STBEverywhere_Back_SharedModels.Data;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_Back_SharedModels.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace STBEverywhere_back_APIClient.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task NotifyPackStatusChange(int clientId, string packType, int packId, string newStatus)
        {
            try
            {
                var title = $"";
                var message = $"";

                if (packType == "Elyssa" || packType == "Student")
                {
                    title = $"Mise à jour de votre  demande sur le pack {packType}";
                    message = $" votre demande sur le pack {packType} a été : {newStatus}";
                }
                else if (packType == "Reclamtion")
                {
                    title = $"Mise à jour de votre réclamation";
                    message = $"vous avez recu une reponse sur votre reclamation";
                }

                var notification = new NotificationPack
                {
                    ClientId = clientId,
                    Title = title,
                    Message = message,
                    IsRead = false,
                    NotificationType = "StatusChange",
                    RelatedPackId = packId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.NotificationsPack.Add(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la notification");
                throw;
            }
        }

        public async Task<IEnumerable<NotificationPack>> GetClientNotifications(int clientId)
        {
            return await _context.NotificationsPack
                .Where(n => n.ClientId == clientId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task MarkAsRead(int notificationId)
        {
            var notification = await _context.NotificationsPack.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task NotifyReclamationStatusChange(int clientId, int reclamationId, string newStatus)
        {
            try
            {
                var title = $"Mise à jour de votre réclamation";
                var message = $"Le statut de votre réclamation a changé: {newStatus}";

                var notification = new NotificationReclamation
                {
                    ClientId = clientId,
                    Title = title,
                    Message = message,
                    IsRead = false,
                    NotificationType = "ReclamationStatusChange",
                    CreatedAt = DateTime.UtcNow
                };

                _context.NotificationsReclamation.Add(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la notification de réclamation");
                throw;
            }
        }

        public async Task<IEnumerable<NotificationReclamation>> GetClientReclamationNotifications(int clientId)
        {
            return await _context.NotificationsReclamation
                .Where(n => n.ClientId == clientId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task MarkReclamationNotificationAsRead(int notificationId)
        {
            var notification = await _context.NotificationsReclamation.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}