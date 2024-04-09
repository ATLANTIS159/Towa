using Towa.ChatGpt.Enums;

namespace Towa.ChatGpt.Services.Interfaces;

public interface IChatGptService
{
    public Task Init();

    public Task<(ResponseReason reason, List<string> messages)> GetChatMessage(ChatPlatform platform,
        string userId, string username,
        string message, bool isOwner = false);

    public Task<string> DeleteChat(string userId);
}