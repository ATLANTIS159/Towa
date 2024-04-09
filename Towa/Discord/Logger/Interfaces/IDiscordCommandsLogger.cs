using ILogger = Serilog.ILogger;

namespace Towa.Discord.Logger.Interfaces;

public interface IDiscordCommandsLogger
{
    public ILogger Log { get; }
}