using Discord.Interactions;
using Towa.ChatGpt.Services.Interfaces;
using Towa.Discord.Logger.Interfaces;

namespace Towa.Discord.Commands;

public class CleanChatCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IChatGptService _chatGptService;
    private readonly IDiscordCommandsLogger _logger;

    public CleanChatCommand(IChatGptService chatGptService, IDiscordCommandsLogger logger)
    {
        _chatGptService = chatGptService;
        _logger = logger;
    }

    [SlashCommand("forget", "Очистить память Товы про разговор с тобой")]
    public async Task CleanChat()
    {
        _logger.Log.Information("Получена команда на очистку истории чата");

        var response = await _chatGptService.DeleteChat(Context.User.Id.ToString());
        var message = $"{Context.User.Mention} {response}";

        await RespondAsync(message, ephemeral: true);
    }
}