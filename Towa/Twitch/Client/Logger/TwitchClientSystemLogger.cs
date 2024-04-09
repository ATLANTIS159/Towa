using Towa.Twitch.Client.Logger.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Twitch.Client.Logger;

public sealed class TwitchClientSystemLogger : ITwitchClientSystemLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public TwitchClientSystemLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    public ILogger Log => _logger;
}