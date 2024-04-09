namespace Towa.Twitch.Api.Models;

public class TwitchStreamInfo
{
    public TwitchStreamInfo(string title, string gameName, int viewersCount, string thumbnailImage)
    {
        Title = title;
        GameName = gameName;
        ViewersCount = viewersCount;
        ThumbnailImage = thumbnailImage;
    }

    public string Title { get; set; }
    public string GameName { get; set; }
    public int ViewersCount { get; set; }
    public string ThumbnailImage { get; set; }
}