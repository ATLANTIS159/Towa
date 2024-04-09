using ILogger = Serilog.ILogger;

namespace Towa.Twitch.PubSub.Logger.Interfaces;

public interface ITwitchPubSubSystemLogger
{
    public ILogger Log { get; }
}