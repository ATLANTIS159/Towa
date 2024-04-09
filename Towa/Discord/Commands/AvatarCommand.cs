using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Towa.Discord.Logger.Interfaces;

namespace Towa.Discord.Commands;

public class AvatarCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly IDiscordCommandsLogger _logger;

    public AvatarCommand(DiscordSocketClient client, IDiscordCommandsLogger logger)
    {
        _client = client;
        _logger = logger;
    }

    [EnabledInDm(false)]
    [SlashCommand("avatar", "Тырим аватарку у других")]
    public async Task GetAvatar(IUser user)
    {
        _logger.Log.Information("Получена команда на получения аватарки пользователя {Target}", user.Username);

        var embed = new EmbedBuilder
        {
            Title = @$"Тырим аватарку у {user.Username}",
            Fields = new List<EmbedFieldBuilder>
            {
                new()
                {
                    Name = "PNG",
                    Value =
                        $"[Ссылка]({user.GetAvatarUrl(ImageFormat.Png, 2048) ?? user.GetDefaultAvatarUrl()})",
                    IsInline = true
                },
                new()
                {
                    Name = "JPG",
                    Value =
                        $"[Ссылка]({user.GetAvatarUrl(ImageFormat.Jpeg, 2048) ?? user.GetDefaultAvatarUrl()})",
                    IsInline = true
                },
                new()
                {
                    Name = "WebP",
                    Value =
                        $"[Ссылка]({user.GetAvatarUrl(ImageFormat.WebP, 2048) ?? user.GetDefaultAvatarUrl()})",
                    IsInline = true
                }
            },
            ImageUrl = user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl(),
            Color = Color.Green,
            Footer = new EmbedFooterBuilder
            {
                IconUrl =
                    _client.CurrentUser.GetAvatarUrl(ImageFormat.Jpeg, 2048) ??
                    _client.CurrentUser.GetDefaultAvatarUrl(),
                Text = _client.CurrentUser.Username
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        await RespondAsync(embed: embed.Build());
        _logger.Log.Information("Команда на получения аватарки пользователя {Target} завершена", user.Username);
    }
}