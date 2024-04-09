using Discord.Interactions;
using Towa.Discord.Handlers.Interfaces;
using Towa.Discord.Logger.Interfaces;

namespace Towa.Discord.Commands;

public class StreamNotificationCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IDiscordHandler _handler;
    private readonly IDiscordCommandsLogger _logger;

    public StreamNotificationCommand(IDiscordHandler handler, IDiscordCommandsLogger logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [EnabledInDm(false)]
    [SlashCommand("notify", "Test notification")]
    public async Task SendNotification()
    {
        _logger.Log.Information("Получена команда на оповещение о начале стрима");
        await DeferAsync(true);

        await _handler.CreateNotification();

        await FollowupAsync("Команда на оповещение о начале стрима завершена", ephemeral: true);
        _logger.Log.Information("Команда на оповещение о начале стрима завершена");
    }
}