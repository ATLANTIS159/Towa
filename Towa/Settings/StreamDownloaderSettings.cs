namespace Towa.Settings;

public class StreamDownloaderSettings
{
    public bool IsDownloaderActive { get; set; } = false;
    public string UniqueId { get; set; } = "Уникальный Id (unique_id) из куков браузера на странице твича после логина";
    public string AuthToken { get; set; } = "Токен (auth-token) из куков браузера на странице твича после логина";
}