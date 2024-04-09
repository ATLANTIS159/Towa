using Towa.Discord.Logger.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Discord.Logger;

public sealed class DiscordCommandsLogger : IDiscordCommandsLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public DiscordCommandsLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public ILogger Log => _logger;

    public void Dispose()
    {
        _logger.Dispose();
    }
}