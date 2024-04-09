using Towa.StreamDownloader.Logger.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.StreamDownloader.Logger;

public sealed class StreamDownloaderLogger : IStreamDownloaderLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public StreamDownloaderLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    public ILogger Log => _logger;
}