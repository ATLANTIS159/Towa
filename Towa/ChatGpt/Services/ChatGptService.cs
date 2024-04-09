using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Towa.ChatGpt.Database;
using Towa.ChatGpt.Enums;
using Towa.ChatGpt.Logger.Interfaces;
using Towa.ChatGpt.Models.Database;
using Towa.ChatGpt.Models.Request;
using Towa.ChatGpt.Models.Response;
using Towa.ChatGpt.Services.Interfaces;
using Towa.Settings;

namespace Towa.ChatGpt.Services;

public class ChatGptService : IChatGptService
{
    private const string ChatUrl = "https://api.openai.com/v1/chat/completions";
    private readonly IDbContextFactory<ChatGptDbContext> _database;
    private readonly IOptionsMonitor<CoreSettings> _settings;
    private readonly IChatGptSystemLogger _systemLogger;

    public ChatGptService(IOptionsMonitor<CoreSettings> settings, IDbContextFactory<ChatGptDbContext> database,
        IChatGptSystemLogger systemLogger)
    {
        _settings = settings;
        _database = database;
        _systemLogger = systemLogger;
    }

    public async Task<(ResponseReason reason, List<string> messages)> GetChatMessage(ChatPlatform platform,
        string userId, string username,
        string message, bool isOwner = false)
    {
        (ResponseReason reason, List<string> messages) outMessage;

        await using var db = await _database.CreateDbContextAsync();
        var chat = db.Chats
            .Include(i => i.Messages)
            .Where(w => w.Messages.Any(a => a.UserId == userId))
            .OrderBy(o => o.Messages.OrderBy(ob => ob.Id).FirstOrDefault()!.Id).FirstOrDefault();

        if (chat is null)
        {
            outMessage = await NewChat(db, platform, userId, username, message, isOwner);
        }
        else
        {
            if (chat.Timestamp.AddHours(5) >= DateTimeOffset.Now)
            {
                outMessage = await EditChat(platform, chat, username, message, isOwner);
            }
            else
            {
                db.Chats.Remove(chat);
                outMessage = await NewChat(db, platform, userId, username, message, isOwner);
            }
        }

        if (outMessage.reason != ResponseReason.Error) await db.SaveChangesAsync();

        return outMessage;
    }

    public async Task<string> DeleteChat(string userId)
    {
        await using var db = await _database.CreateDbContextAsync();
        var chat = db.Chats
            .Include(i => i.Messages).FirstOrDefault(w => w.Messages.Any(a => a.UserId == userId));

        if (chat is null) return "Мы с тобой ничего не обсуждали. Мне не о чем забывать";

        db.Chats.Remove(chat);
        await db.SaveChangesAsync();
        return "Всё, я забыл о чём мы с тобой разговаривали";
    }

    public async Task Init()
    {
        await using var db = await _database.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    private async Task<(ResponseReason reason, List<string> messages)> NewChat(ChatGptDbContext db,
        ChatPlatform platform, string userId,
        string username, string message, bool isOwner = false)
    {
        var chat = new Chat
        {
            UserId = userId,
            Timestamp = DateTimeOffset.Now,
            Messages = new List<ChatMessage>(),
            TotalTokens = 0
        };

        chat.Messages = CreateChatMessagesList(chat, username, message, isOwner, true);

        var chatResponse = await GetChatGptServerResponse(platform, chat);

        if (chatResponse.response is null || !chatResponse.isSuccess)
            return (ResponseReason.Error,
                new List<string>
                    { "У меня не получилось получить для тебя ответ. Что-то пошло не так. Попробуй спросить ещё раз" });

        AddResponseMessageAndUpdate(chat, username, chatResponse.response);

        await db.Chats.AddAsync(chat);

        return (chatResponse.response.Choices.First().FinishReason,
            PrepareMessage(platform, chatResponse.response.Choices.First().Message.Content));
    }

    private async Task<(ResponseReason reason, List<string> messages)> EditChat(ChatPlatform platform, Chat chat,
        string username, string message, bool isOwner)
    {
        chat.Timestamp = DateTimeOffset.Now;

        var messages = CreateChatMessagesList(chat, username, message, isOwner);

        chat.Messages.AddRange(messages);

        var chatResponse = await GetChatGptServerResponse(platform, chat);

        if (chatResponse.response is null || !chatResponse.isSuccess)
            return (ResponseReason.Error,
                new List<string>
                    { "У меня не получилось получить для тебя ответ. Что-то пошло не так. Попробуй спросить ещё раз" });

        AddResponseMessageAndUpdate(chat, username, chatResponse.response);

        return (chatResponse.response.Choices.First().FinishReason,
            PrepareMessage(platform, chatResponse.response.Choices.First().Message.Content));
    }

    private static string GetSystemMessage(bool isOwner, string nickname)
    {
        var systemMessage =
            "Представь себя виртуальной жабкой на канале etaCarinae и веди себя как виртуальная жабка. Тебя зовут Това, а твоего владельца зовут Полина. Она стример";
        systemMessage += isOwner
            ? ". Сейчас тебе пишет твой владелец, Полина. Обращайся к ней по имени"
            : $". Сейчас тебе пишет зритель с канала с ником {nickname}";

        // systemMessage += ". Отвечай коротко";

        return systemMessage;
    }

    private static List<ChatMessage> CreateChatMessagesList(Chat chat, string username, string message, bool isOwner,
        bool isFirstMessages = false)
    {
        return isFirstMessages
            ? new List<ChatMessage>
            {
                new()
                {
                    UserId = chat.UserId,
                    Username = username,
                    Role = MessageRole.System.ToString().ToLower(),
                    Content = GetSystemMessage(isOwner, username),
                    Chat = chat
                },
                new()
                {
                    UserId = chat.UserId,
                    Username = username,
                    Role = MessageRole.User.ToString().ToLower(),
                    Content = message,
                    Chat = chat
                }
            }
            : new List<ChatMessage>
            {
                new()
                {
                    UserId = chat.UserId,
                    Username = username,
                    Role = MessageRole.User.ToString().ToLower(),
                    Content = message,
                    Chat = chat
                }
            };
    }

    private ChatRequest CreateChatRequestMessages(ChatPlatform platform, Chat chat)
    {
        var maxToken = ChatPlatformProcess.GetMaxTokensAmount(platform);

        var chatRequest = new ChatRequest
        {
            Model = _settings.CurrentValue.ChatGpt.ChatGptModel,
            Messages = new List<ChatRequestMessage>(),
            MaxTokens = maxToken,
            Temperature = _settings.CurrentValue.ChatGpt.Temperature,
            PresencePenalty = _settings.CurrentValue.ChatGpt.PresencePenalty
        };

        foreach (var chatMessage in chat.Messages)
            chatRequest.Messages.Add(new ChatRequestMessage
            {
                Role = chatMessage.Role,
                Content = chatMessage.Content
            });

        return chatRequest;
    }

    private async Task<(bool isSuccess, ChatResponse? response)> GetChatGptServerResponse(ChatPlatform platform,
        Chat chat)
    {
        HttpResponseMessage? response = null;

        do
        {
            try
            {
                if (response is not null && !response.IsSuccessStatusCode) chat.Messages.RemoveAt(1);

                var chatRequest = CreateChatRequestMessages(platform, chat);

                using var client = GetHttpClient();
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(chatRequest), Encoding.UTF8,
                    "application/json");

                response = await client.SendAsync(requestMessage);
            }
            catch (Exception e)
            {
                _systemLogger.Log.Error("При получении ответа произошла ошибка. Сообщение ошибки: {Message}",
                    e.Message);
                return (false, new ChatResponse());
            }
        } while (!response.IsSuccessStatusCode);

        return (true, JsonConvert.DeserializeObject<ChatResponse>(await response.Content.ReadAsStringAsync()));
    }

    private static void AddResponseMessageAndUpdate(Chat chat, string username, ChatResponse chatResponse)
    {
        chat.Messages.Add(new ChatMessage
        {
            UserId = chat.UserId,
            Username = username,
            Role = MessageRole.Assistant.ToString().ToLower(),
            Content = chatResponse.Choices.First().Message.Content,
            Chat = chat
        });

        chat.TotalTokens = chatResponse.Usage.TotalTokens;
    }

    private static List<string> PrepareMessage(ChatPlatform platform, string message)
    {
        var chunkSize = ChatPlatformProcess.GetMaxCharactersAmount(platform);
        var chunks = new List<string>();
        var words = message.Split(' ');
        var chunk = string.Empty;

        foreach (var word in words)
        {
            if ((chunk + word).Length > chunkSize)
            {
                chunks.Add(chunk.Trim());
                chunk = string.Empty;
            }

            chunk = string.Join(" ", chunk, word);
        }

        if (!string.IsNullOrEmpty(chunk)) chunks.Add(chunk.Trim());

        return chunks;
    }

    private HttpClient GetHttpClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(3);

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.CurrentValue.ChatGpt.Token}");

        return client;
    }
}