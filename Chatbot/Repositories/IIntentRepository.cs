using Chatbot.Models;

namespace Chatbot.Repositories
{
    public interface IIntentRepository
    {
        Task<ChatbotData> GetIntentsAsync();
        Task UpdateIntentAsync(Intent updatedIntent);
        Task ReloadDataAsync();
    }
}
