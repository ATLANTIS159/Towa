using ILogger = Serilog.ILogger;

namespace Towa.Twitch.Api.Logger.Interfaces;

public interface ITwitchApiLogger
{
    public ILogger Log { get; }
}