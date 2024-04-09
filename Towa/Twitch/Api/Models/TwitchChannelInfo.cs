namespace Towa.Twitch.Api.Models;

public class TwitchChannelInfo
{
    public TwitchChannelInfo(string title)
    {
        Title = title;
    }

    public string Title { get; set; }
}