namespace Towa.Settings;

public class DiscordSettings
{
    public string Token { get; set; } = "Токен доступа дискорд бота";
    public string ServerId { get; set; } = "Сервер к которому подключается бот";
    public string Database { get; set; } = "Discord.etaDB";
    public string FollowerRoleId { get; set; } = "ID дискорд роли которая будет присваиваться новым участрикам";
    public bool IsNotificationsActive { get; set; } = false;
    public string NotificationChannelId { get; set; } = "ID дискорд канала где будут уведомления о стримах";
    public string GiveawayEmote { get; set; } = ":frog:";
    public int UtcHourCorrection { get; set; } = 3;
}