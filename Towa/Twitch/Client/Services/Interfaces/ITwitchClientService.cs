namespace Towa.Twitch.Client.Services.Interfaces;

public interface ITwitchClientService
{
    public Task StartTwitchClient();
    public void SendMessage(string author, string message);
}