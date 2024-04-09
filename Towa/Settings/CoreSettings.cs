namespace Towa.Settings;

public class CoreSettings
{
    public DiscordSettings Discord { get; set; } = new();
    public TwitchSettings Twitch { get; set; } = new();
    public StreamDownloaderSettings StreamDownloaderSettings { get; set; } = new();
    public ChatGptSettings ChatGpt { get; set; } = new();
    public RiotSettings Riot { get; set; } = new();
}