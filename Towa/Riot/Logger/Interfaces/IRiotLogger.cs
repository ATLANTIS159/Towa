using ILogger = Serilog.ILogger;

namespace Towa.Riot.Logger.Interfaces;

public interface IRiotLogger
{
    public ILogger Log { get; }
}