namespace Towa.Settings;

public class ChatGptSettings
{
    public bool IsActiveInTwitch { get; set; } = false;
    public bool IsActiveInDiscord { get; set; } = false;
    public string Token { get; set; } = "API ключ OpenAI";
    public string Database { get; set; } = "ChatGPT.etaDB";
    public string ChatGptModel { get; set; } = "gpt-3.5-turbo";
    public double Temperature { get; set; } = 0.35;
    public double PresencePenalty { get; set; } = 1.2;
}