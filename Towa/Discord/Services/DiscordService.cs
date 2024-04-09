using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog.Events;
using Towa.Discord.Database;
using Towa.Discord.Handlers.Interfaces;
using Towa.Discord.Logger.Interfaces;
using Towa.Discord.Services.Interfaces;
using Towa.Settings;

namespace Towa.Discord.Services;

public class DiscordService : IDiscordService
{
    private readonly DiscordSocketClient _client;
    private readonly IDbContextFactory<DiscordDbContext> _database;
    private readonly IDiscordHandler _handler;
    private readonly IDiscordSystemLogger? _logger;
    private readonly IOptionsMonitor<CoreSettings> _settings;

    public DiscordService(IDbContextFactory<DiscordDbContext> database, DiscordSocketClient client,
        IDiscordHandler handler,
        IOptionsMonitor<CoreSettings> settings, IDiscordSystemLogger? logger)
    {
        _database = database;
        _client = client;
        _handler = handler;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartService()
    {
        _logger!.Log.Information("Запуск систем Дискорда");

        await using var db = await _database.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        _client.Log += LogAsync;

        await _handler.InitializeAsync();

        await _client.LoginAsync(TokenType.Bot, _settings.CurrentValue.Discord.Token);
        await _client.StartAsync();

        await Task.Delay(Timeout.Infinite);
    }

    private Task LogAsync(LogMessage message)
    {
        var severity = message.Severity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => LogEventLevel.Information
        };

        _logger?.Log.Write(severity, "[{MessageSource}] {MessageMessage}", message.Source, message.Message);
        return Task.CompletedTask;
    }
}