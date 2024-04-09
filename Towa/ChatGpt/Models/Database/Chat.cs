using System.ComponentModel.DataAnnotations;

namespace Towa.ChatGpt.Models.Database;

public class Chat
{
    [Key] public int Id { get; set; }
    public string UserId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public List<ChatMessage> Messages { get; set; }
    public int TotalTokens { get; set; }
}

public class ChatMessage
{
    [Key] public int Id { get; set; }
    public string UserId { get; set; }
    public string Username { get; set; }
    public string Role { get; init; }
    public string Content { get; init; }
    public Chat Chat { get; init; }
}