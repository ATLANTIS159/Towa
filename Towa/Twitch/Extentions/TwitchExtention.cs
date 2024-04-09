using System.Text;
using Serilog;
using Serilog.Events;
using Towa.Twitch.Api.Logger;
using Towa.Twitch.Api.Logger.Interfaces;
using Towa.Twitch.Api.Services;
using Towa.Twitch.Api.Services.Interfaces;
using Towa.Twitch.Client.Logger;
using Towa.Twitch.Client.Logger.Interfaces;
using Towa.Twitch.Client.Services;
using Towa.Twitch.Client.Services.Interfaces;
using Towa.Twitch.PubSub.Logger;
using Towa.Twitch.PubSub.Logger.Interfaces;
using Towa.Twitch.PubSub.Services;
using Towa.Twitch.PubSub.Services.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Twitch.Extentions;

public static class TwitchExtention
{
    private static readonly string AppPath = $"{AppContext.BaseDirectory}";

    public static IServiceCollection AddTwitch(this IServiceCollection services, ILogger coreLogger)
    {
        coreLogger.Information("Инициализация систем Твича");

        services.AddSingleton(InitTwitchApiLogger());
        services.AddSingleton(InitTwitchClientSystemLogger());
        services.AddSingleton(InitTwitchClientChatLogger());
        services.AddSingleton(InitTwitchPubSubSystemLogger());
        services.AddSingleton<ITwitchApiService, TwitchApiService>();
        services.AddSingleton<ITwitchClientService, TwitchClientService>();
        services.AddSingleton<ITwitchPubSubService, TwitchPubSubService>();

        coreLogger.Information("Инициализация систем Твича завершена");

        return services;
    }

    private static ITwitchApiLogger InitTwitchApiLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new TwitchApiLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(TwitchApiLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File($"{AppPath}Logs/Twitch/Api/TwitchApi_.etaLogs",
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }

    private static ITwitchClientSystemLogger InitTwitchClientSystemLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new TwitchClientSystemLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(TwitchClientSystemLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File($"{AppPath}Logs/Twitch/Client/System/TwitchClientSystem_.etaLogs",
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }

    private static ITwitchClientChatLogger InitTwitchClientChatLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new TwitchClientChatLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(TwitchClientChatLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File($"{AppPath}Logs/Twitch/Client/Chat/TwitchClientChat_.etaLogs",
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }

    private static ITwitchPubSubSystemLogger InitTwitchPubSubSystemLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new TwitchPubSubSystemLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(TwitchPubSubSystemLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File($"{AppPath}Logs/Twitch/PubSub/System/TwitchPubSubSystem_.etaLogs",
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }
}