using SmallBot.Models;

namespace SmallBot.Services
{
    public interface IOpenAiService
    {
        Task<string> GetCompletionAsync(string prompt, int maxTokens = 150, double temperature = 0.9);

        Task<Message> GetChatCompletionAsync(List<Message> messageList, int maxTokens = 4096, double temperature = 0.9);
    }
}
