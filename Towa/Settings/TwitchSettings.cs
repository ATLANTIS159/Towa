namespace Towa.Settings;

public class TwitchSettings
{
    public string BotName { get; set; } = "Ник бота на твиче";
    public string ClientId { get; set; } = "ClientID бота из панели разработчика";
    public string OAuthKey { get; set; } = "Ключ доступа бота к каналу";
    public string JoinChannel { get; set; } = "Канал к которому подключается бот";
    public bool IsCommandsEnabled { get; set; } = false;
}