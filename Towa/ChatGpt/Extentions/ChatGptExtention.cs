using System.Text;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Towa.ChatGpt.Database;
using Towa.ChatGpt.Logger;
using Towa.ChatGpt.Logger.Interfaces;
using Towa.ChatGpt.Services;
using Towa.ChatGpt.Services.Interfaces;
using ILogger = Serilog.ILogger;

namespace Towa.ChatGpt.Extentions;

public static class ChatGptExtention
{
    private static readonly string DatabasePath = Path.Combine(AppContext.BaseDirectory, "Database");

    public static void AddChatGpt(this IServiceCollection services, WebApplicationBuilder builder,
        ILogger coreLogger)
    {
        coreLogger.Information("Инициализация систем ChatGpt");

        services.AddSingleton(InitChatGptSystemLogger());
        services.AddDbContextFactory<ChatGptDbContext>(f =>
            f.UseSqlite($"Data Source={DatabasePath}/{builder.Configuration["ChatGpt:Database"]}"));

        services.AddSingleton<IChatGptService, ChatGptService>();

        coreLogger.Information("Инициализация систем ChatGpt завершена");
    }

    private static IChatGptSystemLogger InitChatGptSystemLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new ChatGptSystemLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(ChatGptSystemLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs/ChatGpt/System/ChatGptSystem_.etaLogs"),
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }
}