using Towa.Twitch.Api.Logger.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Twitch.Api.Logger;

public sealed class TwitchApiLogger : ITwitchApiLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public TwitchApiLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    public ILogger Log => _logger.ForContext<TwitchApiLogger>();
}