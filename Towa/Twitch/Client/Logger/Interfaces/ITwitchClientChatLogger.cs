using ILogger = Serilog.ILogger;

namespace Towa.Twitch.Client.Logger.Interfaces;

public interface ITwitchClientChatLogger
{
    public ILogger Log { get; }
}