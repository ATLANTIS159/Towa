using ILogger = Serilog.ILogger;

namespace Towa.Twitch.Client.Logger.Interfaces;

public interface ITwitchClientSystemLogger
{
    public ILogger Log { get; }
}