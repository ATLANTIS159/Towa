using Newtonsoft.Json;

namespace Towa.ChatGpt.Models.Request;

public class ChatRequest
{
    [JsonProperty("model")] public string Model { get; set; }

    [JsonProperty("messages")] public List<ChatRequestMessage> Messages { get; set; }

    [JsonProperty("max_tokens")] public int MaxTokens { get; set; }

    [JsonProperty("temperature")] public double Temperature { get; set; }

    [JsonProperty("presence_penalty")] public double PresencePenalty { get; set; }
}

public class ChatRequestMessage
{
    [JsonProperty("role")] public string Role { get; set; }

    [JsonProperty("content")] public string Content { get; set; }
}