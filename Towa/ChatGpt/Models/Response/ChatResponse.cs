using Newtonsoft.Json;
using Towa.ChatGpt.Enums;

namespace Towa.ChatGpt.Models.Response;

public class ChatResponse
{
    [JsonProperty("created")] public int CreatedAt { get; set; }

    [JsonProperty("choices")] public List<ChatResponseChoice> Choices { get; set; }

    [JsonProperty("usage")] public ChatResponseUsage Usage { get; set; }
}

public class ChatResponseChoice
{
    public ChatResponseChoice(ChatResponseMessage message, ResponseReason finishReason)
    {
        Message = message;
        FinishReason = finishReason;
    }

    [JsonProperty("message")] public ChatResponseMessage Message { get; set; }

    [JsonProperty("finish_reason")] public ResponseReason FinishReason { get; set; }
}

public class ChatResponseMessage
{
    public ChatResponseMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    [JsonProperty("role")] public string Role { get; set; }

    [JsonProperty("content")] public string Content { get; set; }
}

public class ChatResponseUsage
{
    [JsonProperty("total_tokens")] public int TotalTokens { get; set; }
}