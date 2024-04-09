using Towa.Riot.Logger.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Riot.Logger;

public sealed class RiotLogger : IRiotLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public RiotLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    public ILogger Log => _logger;
}