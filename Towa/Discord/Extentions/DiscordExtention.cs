using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Towa.Discord.Database;
using Towa.Discord.Handlers;
using Towa.Discord.Handlers.Interfaces;
using Towa.Discord.Logger;
using Towa.Discord.Logger.Interfaces;
using Towa.Discord.Services;
using Towa.Discord.Services.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.Discord.Extentions;

public static class DiscordExtention
{
    private static readonly DiscordSocketConfig SocketConfig = new()
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent |
                         GatewayIntents.DirectMessages | GatewayIntents.DirectMessageTyping,
        AlwaysDownloadUsers = true,
        LogGatewayIntentWarnings = false,
        LogLevel = LogSeverity.Info
    };

    private static readonly string DatabasePath = Path.Combine(AppContext.BaseDirectory, "Database");

    public static IServiceCollection AddDiscord(this IServiceCollection services, WebApplicationBuilder builder,
        ILogger coreLogger)
    {
        coreLogger.Information("Инициализация систем Дискорда");

        services.AddSingleton(InitDiscordSystemLogger());
        services.AddSingleton(InitDiscordCommandsLogger());
        services.AddDbContextFactory<DiscordDbContext>(f =>
            f.UseSqlite($"Data Source={DatabasePath}/{builder.Configuration["Discord:Database"]}"));
        services.AddSingleton(SocketConfig);
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(),
            new InteractionServiceConfig
            {
                UseCompiledLambda = true
            }));
        services.AddSingleton<IDiscordHandler, DiscordHandler>();
        services.AddSingleton<IDiscordService, DiscordService>();

        coreLogger.Information("Инициализация систем Дискорда завершена");

        return services;
    }

    private static IDiscordSystemLogger InitDiscordSystemLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new DiscordSystemLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(DiscordSystemLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs/Discord/System/DiscordSystems_.etaLogs"),
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }

    private static IDiscordCommandsLogger InitDiscordCommandsLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new DiscordCommandsLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(DiscordCommandsLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs/Discord/Commands/DiscordCommands_.etaLogs"),
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }
}