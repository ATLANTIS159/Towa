using ILogger = Serilog.ILogger;

namespace Towa.StreamDownloader.Logger.Interfaces;

public interface IStreamDownloaderWoConsoleLogger
{
    public ILogger Log { get; }
}