using Towa.Twitch.PubSub.Logger.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Twitch.PubSub.Logger;

public sealed class TwitchPubSubSystemLogger : ITwitchPubSubSystemLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public TwitchPubSubSystemLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    public ILogger Log => _logger;
}