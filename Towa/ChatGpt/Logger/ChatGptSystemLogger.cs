using Towa.ChatGpt.Logger.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.ChatGpt.Logger;

public sealed class ChatGptSystemLogger : IChatGptSystemLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public ChatGptSystemLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public ILogger Log => _logger;

    public void Dispose()
    {
        _logger.Dispose();
    }
}