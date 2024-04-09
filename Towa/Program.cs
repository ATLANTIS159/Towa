using System.Text;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using Towa.ChatGpt.Extentions;
using Towa.Discord.Extentions;
using Towa.Riot.Extentions;
using Towa.Settings;
using Towa.StreamDownloader.Extentions;
using Towa.Twitch.Extentions;

namespace Towa;

public static class Program
{
    private static readonly string AppPath = $"{AppContext.BaseDirectory}";

    private static readonly string ConfigPath = Path.Combine(AppPath, "Config");
    private static readonly string DatabasePath = Path.Combine(AppPath, "Database");

    private static readonly string ConfigFile = Path.Combine(ConfigPath, "CoreSettings.etaConfig");
    private static readonly string TwitchModersFile = Path.Combine(ConfigPath, "TwitchModersList.etaConfig");

    private static readonly string TwitchCustomCommandsFile =
        Path.Combine(ConfigPath, "TwitchCustomCommands.etaConfig");

    public static void Main(string[] args)
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(Program))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File($"{AppPath}Logs/Core/Core_.etaLogs", LogEventLevel.Information, template,
                rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateBootstrapLogger();

        Log.Information("Запущена инициализация Товы");

        if (!Directory.Exists(ConfigPath))
        {
            Directory.CreateDirectory(ConfigPath);
            Log.Information("Создана папка конфигураций");
        }

        if (!Directory.Exists(DatabasePath))
        {
            Directory.CreateDirectory(DatabasePath);
            Log.Information("Создана папка базы данных");
        }

        Log.Information("Папки конфигураций и базы данных найдены");

        if (!File.Exists(TwitchModersFile))
        {
            Log.Warning("Файл списка Твич модераторов не найден");
            File.WriteAllText(TwitchModersFile,
                JsonConvert.SerializeObject(
                    new TwitchModersList { ModersList = new List<string> { "Moder1", "Moder2" } }, Formatting.Indented),
                Encoding.UTF8);
            Log.Warning("Создан новый файл списка Твич модераторов. Не забудьте заполнить его");
        }

        if (!File.Exists(TwitchCustomCommandsFile))
        {
            Log.Warning("Файл списка кастомных команд Твич не найден");
            File.WriteAllText(TwitchCustomCommandsFile,
                JsonConvert.SerializeObject(
                    new TwitchCustomCommands { CustomCommands = new List<CustomCommand> { new() } },
                    Formatting.Indented),
                Encoding.UTF8);
            Log.Warning("Создан новый файл списка кастомных команд Твич. Не забудьте заполнить его");
        }

        if (!File.Exists(ConfigFile))
        {
            Log.Warning("Файл конфигураций не найден");
            File.WriteAllText(ConfigFile, JsonConvert.SerializeObject(new CoreSettings(), Formatting.Indented),
                Encoding.UTF8);
            Log.Error(
                "Сгенерирован новый файл конфигураций в папке Config. Закройте программу, заполните файл и перезапустите");
            Console.ReadLine();
            return;
        }

        Log.Information("Инициализация основных систем");
        var builder = WebApplication.CreateBuilder(args);
        var services = builder.Services;

        builder.Host.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddJsonFile(ConfigFile, false, true);
            config.AddJsonFile(TwitchModersFile, false, true);
            config.AddJsonFile(TwitchCustomCommandsFile, false, true);
        });
        builder.Host.UseSerilog();

        services.Configure<CoreSettings>(builder.Configuration);
        services.Configure<TwitchModersList>(builder.Configuration);
        services.Configure<TwitchCustomCommands>(builder.Configuration);

        Log.Information("Файл конфигураций успешно загружен");

        services.AddStreamDownloader(Log.Logger);
        services.AddChatGpt(builder, Log.Logger);
        services.AddRiot(Log.Logger);
        services.AddTwitch(Log.Logger);
        services.AddDiscord(builder, Log.Logger);
        services.AddSingleton<Core>();

        var app = builder.Build();

        app.Services.GetRequiredService<Core>().InitCore();

        app.Run();
    }
}