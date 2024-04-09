using ILogger = Serilog.ILogger;

namespace Towa.StreamDownloader.Logger.Interfaces;

public interface IStreamDownloaderLogger
{
    public ILogger Log { get; }
}