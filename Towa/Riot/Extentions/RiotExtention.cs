using System.Text;
using Serilog;
using Serilog.Events;
using Towa.Riot.Logger;
using Towa.Riot.Logger.Interfaces;
using Towa.Riot.Services;
using Towa.Riot.Services.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Riot.Extentions;

public static class RiotExtention
{
    private static readonly string AppPath = $"{AppContext.BaseDirectory}";

    public static IServiceCollection AddRiot(this IServiceCollection services, ILogger coreLogger)
    {
        coreLogger.Information("Инициализация систем Riot");

        services.AddSingleton(InitRiotLogger());
        services.AddSingleton<IRiotService, RiotService>();

        coreLogger.Information("Инициализация систем Riot завершена");

        return services;
    }

    private static IRiotLogger InitRiotLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new RiotLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(RiotLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File(Path.Combine(AppPath, "Logs/Riot/Riot_.etaLogs"),
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }
}