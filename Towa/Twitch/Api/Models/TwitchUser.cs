namespace Towa.Twitch.Api.Models;

public class TwitchUser
{
    public string? Login { get; set; }
    public string? DisplayName { get; set; }
    public string? Id { get; set; }
    public string? ProfileImage { get; set; }
    public DateTime CreatedAt { get; set; }
}