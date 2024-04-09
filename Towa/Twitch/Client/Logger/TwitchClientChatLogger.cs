using Towa.Twitch.Client.Logger.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Twitch.Client.Logger;

public sealed class TwitchClientChatLogger : ITwitchClientChatLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public TwitchClientChatLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    public ILogger Log => _logger;
}