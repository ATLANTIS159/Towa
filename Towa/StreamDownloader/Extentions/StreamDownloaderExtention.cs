using System.Text;
using Serilog;
using Serilog.Events;
using Towa.StreamDownloader.Logger;
using Towa.StreamDownloader.Logger.Interfaces;
using Towa.StreamDownloader.Services;
using Towa.StreamDownloader.Services.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.StreamDownloader.Extentions;

public static class StreamDownloaderExtention
{
    private static readonly string AppPath = $"{AppContext.BaseDirectory}";

    public static IServiceCollection AddStreamDownloader(this IServiceCollection services, ILogger coreLogger)
    {
        coreLogger.Information("Инициализация систем загрузчика стримов");

        services.AddSingleton(InitStreamDownloader());
        services.AddSingleton(InitStreamDownloaderWoConsole());
        services.AddSingleton<IStreamDownloaderService, StreamDownloaderService>();

        coreLogger.Information("Инициализация систем загрузчика стримов завершена");

        return services;
    }

    private static IStreamDownloaderLogger InitStreamDownloader()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new StreamDownloaderLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(StreamDownloaderLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File($"{AppPath}Logs/Downloader/Downloader_.etaLogs",
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }

    private static IStreamDownloaderWoConsoleLogger InitStreamDownloaderWoConsole()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new StreamDownloaderWoConsoleLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(StreamDownloaderWoConsoleLogger))
            .WriteTo.File($"{AppPath}Logs/Downloader/Downloader_.etaLogs",
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }
}