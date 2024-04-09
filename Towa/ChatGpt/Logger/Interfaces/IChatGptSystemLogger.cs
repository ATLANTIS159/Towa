using ILogger = Serilog.ILogger;

namespace Towa.ChatGpt.Logger.Interfaces;

public interface IChatGptSystemLogger
{
    public ILogger Log { get; }
}