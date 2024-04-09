using ILogger = Serilog.ILogger;

namespace Towa.Discord.Logger.Interfaces;

public interface IDiscordSystemLogger
{
    public ILogger Log { get; }
}