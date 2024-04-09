using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog.Events;
using Towa.ChatGpt.Enums;
using Towa.ChatGpt.Services.Interfaces;
using Towa.Discord.Database;
using Towa.Discord.Enums;
using Towa.Discord.Handlers.Interfaces;
using Towa.Discord.Logger.Interfaces;
using Towa.Discord.Models;
using Towa.Settings;
using Towa.Twitch.Api.Models;
using Towa.Twitch.Api.Services.Interfaces;

namespace Towa.Discord.Handlers;

public class DiscordHandler : IDiscordHandler
{
    private const string NotificationLoadingGif =
        "https://cdn.discordapp.com/attachments/429656151949443092/831563836561293312/Loading.gif";

    private const string NotificationDefaultGif = "https://i.imgur.com/7MxlAf7.gifv";
    private readonly IChatGptService _chatGptService;
    private readonly IDiscordCommandsLogger _commandsLogger;
    private readonly IDbContextFactory<DiscordDbContext> _database;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _services;
    private readonly IOptionsMonitor<CoreSettings> _settings;
    private readonly IDiscordSystemLogger? _systemLogger;
    private readonly ITwitchApiService _twitchApiService;
    private RestApplication _botInfo = null!;

    public DiscordHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider services,
        IOptionsMonitor<CoreSettings> settings, IDbContextFactory<DiscordDbContext> database,
        IDiscordSystemLogger systemLogger,
        IDiscordCommandsLogger commandsLogger, IChatGptService chatGptService, ITwitchApiService twitchApiService)
    {
        Client = client;
        _interactionService = interactionService;
        _services = services;
        _settings = settings;
        _database = database;
        _systemLogger = systemLogger;
        _commandsLogger = commandsLogger;
        _chatGptService = chatGptService;
        _twitchApiService = twitchApiService;
    }

    public DiscordSocketClient Client { get; set; }

    public List<UpdaterItem> UpdaterItems { get; set; } = new();

    public bool StreamUp { get; set; } = true;

    public async Task InitializeAsync()
    {
        Client.Ready += ReadyAsync;
        _interactionService.Log += LogAsync;
        Client.UserJoined += UserJoinedAsync;
        Client.MessageReceived += MessageReceivedAsync;

        await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        Client.InteractionCreated += Interaction;
    }

    private async Task UserJoinedAsync(SocketGuildUser user)
    {
        var role = user.Guild.GetRole(ulong.Parse(_settings.CurrentValue.Discord.FollowerRoleId));

        if (role is not null)
            try
            {
                await user.AddRoleAsync(role);
                _systemLogger!.Log.Information("Роль фолловера выдана {Username}", user.Username);
            }
            catch (Exception error)
            {
                _systemLogger!.Log.Error("Не удалось выдать роль для {Username}. Возникла следующая ошибка: {Error}",
                    user.Username, error.Message);
            }
    }

    private async Task Interaction(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(Client, interaction);
            var result = await _interactionService.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        await interaction.RespondAsync("У тебя нет прав на выполнение этой команды!", ephemeral: true);
                        break;
                }
        }
        catch
        {
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
        }
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

        _systemLogger?.Log.Write(severity, message.Exception, "[{MessageSource}] {MessageMessage}", message.Source,
            message.Message);
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
#if DEBUG
        _systemLogger!.Log.Information("Регистрация комманд для сервера {DiscordServer}",
            _settings.CurrentValue.Discord.ServerId);
        await _interactionService.RegisterCommandsToGuildAsync(
            ulong.Parse(_settings.CurrentValue.Discord.ServerId));
#else
        _systemLogger!.Log.Information("Регистрация глобальных комманд");
        await _interactionService.RegisterCommandsGloballyAsync();
#endif

        _systemLogger.Log.Information("Регистрация комманд завершена");

        _ = InitiateUpdaters();
        _ = UpdateRolesOnStart();

        _botInfo = await Client.GetApplicationInfoAsync();

        _systemLogger.Log.Information("Cистемы Дискорда работают на боте под ником {Nickname}", _botInfo.Name);

        _systemLogger.Log.Information("Запуск систем Дискорда завершён");
    }

    #region System

    private async Task UpdateRolesOnStart()
    {
        var role = Client.GetGuild(ulong.Parse(_settings.CurrentValue.Discord.ServerId))
            .GetRole(ulong.Parse(_settings.CurrentValue.Discord.FollowerRoleId));

        if (role is not null)
        {
            _systemLogger!.Log.Information("Проверка наличия необходимой роли фолловера у участников сервера");

            var users = Client.GetGuild(ulong.Parse(_settings.CurrentValue.Discord.ServerId)).Users
                .Where(u => !u.Roles.Contains(role) && !u.IsBot).ToList();

            if (users.Any())
                foreach (var user in users)
                    try
                    {
                        await user.AddRoleAsync(role);
                        _systemLogger.Log.Information("Роль фолловера выдана {Username}", user.Username);
                    }
                    catch (Exception error)
                    {
                        _systemLogger.Log.Error(
                            "Не удалось выдать роль для {Username}. Возникла следующая ошибка: {Error}", user.Username,
                            error.Message);
                    }

            _systemLogger.Log.Information(
                "Проверка наличия необходимой роли фолловера у участников сервера завершена");
        }
    }

    #endregion

    #region ChatGpt

    private Task MessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot || !_settings.CurrentValue.ChatGpt.IsActiveInDiscord) return Task.CompletedTask;

        if (message.MentionedUsers.Any(m => m.Id == _botInfo.Id) || message.Channel is IDMChannel)
            _ = ProcessChatMessage(message);

        return Task.CompletedTask;
    }

    private async Task ProcessChatMessage(SocketMessage message)
    {
        var typing = message.Channel.EnterTypingState();

        var mention = message.Channel is IDMChannel ? "" : $"{message.Author.Mention}\n";

        var response = await _chatGptService.GetChatMessage(ChatPlatform.Discord,
            message.Author.Id.ToString(), message.Author.Username, FixMessage(message),
            280628803351478274 == message.Author.Id);

        switch (response.reason)
        {
            case ResponseReason.Error:
            case ResponseReason.Stop:
                foreach (var responseMessage in response.messages)
                    await message.Channel.SendMessageAsync(string.Concat(mention, responseMessage));
                break;
            case ResponseReason.Length:
                foreach (var responseMessage in response.messages)
                    await message.Channel.SendMessageAsync(string.Concat(mention, responseMessage));

                await message.Channel.SendMessageAsync(string.Concat(mention,
                    "У меня не влезло всё сообщение. Попроси меня продолжить отправив слово 'Продолжи'"));
                break;
        }

        typing.Dispose();
    }

    private static string FixMessage(IMessage message)
    {
        return message.Tags.Aggregate(message.Content, (current, tag) => tag.Type switch
        {
            TagType.UserMention => current.Replace(current.Contains("<@!") ? $"<@!{tag.Key}>" : $"<@{tag.Key}>",
                string.Empty),
            TagType.ChannelMention => current.Replace($"<#{tag.Key}>", string.Empty),
            TagType.RoleMention => current.Replace($"<@&{tag.Key}>", string.Empty),
            TagType.EveryoneMention => current.Replace("@everyone", string.Empty),
            TagType.HereMention => current.Replace("@here", string.Empty),
            TagType.Emoji => current.Replace($"<:{((Emote)tag.Value).Name}:{tag.Key}>", string.Empty),
            _ => current
        }).TrimStart().TrimEnd();
    }

    #endregion

    #region Giveaway

    public async Task<EmbedBuilder> CreateGiveawayForm(GiveawayItem giveawayItem,
        bool isEnded = false)
    {
        var embed = new EmbedBuilder();

        embed.WithTitle($"**{giveawayItem.Title}**");
        embed.WithColor(Color.Green);
        embed.WithDescription(giveawayItem.Description);

        if (!isEnded)
            embed.AddField("**Окончание розыгрыша:**",
                giveawayItem.IsInfinite
                    ? "__Скоро__"
                    : $"__<t:{giveawayItem.Timestamp}:d> <t:{giveawayItem.Timestamp}:t>\n(<t:{giveawayItem.Timestamp}:R>)__",
                true);
        else
            embed.AddField("**Окончание розыгрыша:**",
                "__Завершён__", true);

        embed.AddField("**Количество призовых мест:**", $"__{giveawayItem.WinnerCount}__", true);

        if (isEnded)
        {
            var winners = await SelectWinners(giveawayItem.ChannelId, giveawayItem.MessageId, giveawayItem.WinnerCount);
            string winnersString;

            if (winners is { Count: 1 })
            {
                winnersString = winners.First().Mention.Replace("!", "");
                embed.AddField("**Победитель:**", winnersString, true);
            }
            else
            {
                var mentionList = winners?.Select(winner => winner.Mention.Replace("!", "")).ToList();

                if (mentionList != null && mentionList.Any())
                {
                    winnersString = string.Join(", ", mentionList);
                    embed.AddField("**Победители:**", winnersString, true);
                }
                else
                {
                    embed.AddField("**Победители:**",
                        "Недостаточно участников", true);
                }
            }

            _commandsLogger.Log.Information("Выбраны победители для розыгрыша {Id}: {Winners}", giveawayItem.MessageId,
                winners);
        }

        if (!string.IsNullOrWhiteSpace(giveawayItem.Image))
            embed.WithImageUrl(giveawayItem.Image);

        embed.WithFooter(!isEnded
            ? "Жмякай жабку!"
            : "Розыгрыш завершён");

        return embed;
    }

    private async Task<List<IUser>?> SelectWinners(ulong channelId, ulong messageId, int count)
    {
        var winners = new List<IUser>();
        List<IUser>? userList = null;

        if (Client.GetChannel(channelId) is not IMessageChannel channel) return winners;

        var message = await channel.GetMessageAsync(messageId);

        try
        {
            userList = (await message
                .GetReactionUsersAsync(Emoji.Parse(_settings.CurrentValue.Discord.GiveawayEmote), 1000)
                .FlattenAsync()).ToList();
        }
        catch
        {
            _systemLogger?.Log.Error("Сообщение с розыгрышем не найдено");
        }

        if (userList is null) return null;

        var bots = userList.Where(u => u.IsBot).ToList();
        foreach (var bot in bots) userList.Remove(bot);

        var random = new Random();

        if (userList.Count < count)
        {
            winners = userList;
            return winners;
        }

        for (var i = 0; i < count; i++)
        {
            var index = random.Next(userList.Count);
            winners.Add(userList[index]);
        }

        return winners;
    }

    private Task InitiateUpdaters()
    {
        _commandsLogger.Log.Information("Инициализация обновления розыгрышей");
        using (var db = _database.CreateDbContext())
        {
            foreach (var giveawayItem in db.GiveawayItems) _ = CheckUpdater(giveawayItem);
        }

        _commandsLogger.Log.Information("Инициализация обновления розыгрышей завершена");
        return Task.CompletedTask;
    }

    public Task CheckUpdater(GiveawayItem giveawayItem)
    {
        if (giveawayItem.IsInfinite || giveawayItem.IsOver) return Task.CompletedTask;

        CheckTime(DateTimeOffset.Now, giveawayItem.Timestamp, out var isOver);

        if (isOver)
        {
            _ = EndGiveaway(giveawayItem.MessageId);
            return Task.CompletedTask;
        }

        var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;
        var updaterItem = new UpdaterItem
        {
            MessageId = giveawayItem.MessageId,
            TokenSource = tokenSource
        };
        UpdaterItems.Add(updaterItem);

        _ = StartUpdater(giveawayItem, token);
        return Task.CompletedTask;
    }

    private async Task StartUpdater(GiveawayItem item, CancellationToken token)
    {
        _systemLogger!.Log.Information("Запущено обновление розыгрыша {Id}", item.MessageId);
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(token))
        {
            CheckTime(DateTimeOffset.Now, item.Timestamp, out var isOver);
            if (isOver) break;
        }

        _ = EndGiveaway(item.MessageId);
    }

    private static void CheckTime(DateTimeOffset currentTime, long endTimestamp, out bool isOver)
    {
        var currentTimestamp = currentTime.ToUnixTimeSeconds();
        isOver = endTimestamp < currentTimestamp;
    }

    public async Task<bool> EndGiveaway(ulong messageId)
    {
        await using var db = await _database.CreateDbContextAsync();

        var giveawayItem = await db.GiveawayItems.FindAsync(messageId);

        if (giveawayItem is null)
        {
            _commandsLogger.Log.Error("Завершение розыгрыша {Id} отменено. Розыгрыш не найден в базе данных",
                messageId);
            return false;
        }

        if (Client.GetChannel(giveawayItem.ChannelId) is not IMessageChannel channel) return false;

        if (giveawayItem.IsOver)
        {
            _commandsLogger.Log.Error("Завершение розыгрыша {Id} отменено. Данный розыгрыш уже закончен",
                giveawayItem.MessageId);
            return false;
        }

        var updaterItem = UpdaterItems.Find(u => u.MessageId == giveawayItem.MessageId);

        if (updaterItem is not null)
        {
            updaterItem.TokenSource.Cancel();
            UpdaterItems.Remove(updaterItem);
            _commandsLogger.Log.Information("Процесс обновления розыгрыша {Id} остановлен и удалён",
                giveawayItem.MessageId);
        }

        EmbedBuilder? form;

        try
        {
            form = await CreateGiveawayForm(giveawayItem, true).ConfigureAwait(false);
            _commandsLogger.Log.Information("Обновлённая форма розыгрыша {Id} создана", giveawayItem.MessageId);
        }
        catch (Exception e)
        {
            if (e.Message != "Message not found")
            {
                _commandsLogger.Log.Error(
                    "При обновлении розыгрыша {Id} произошла ошибка. Сообщение с таким Id не найдено",
                    giveawayItem.MessageId);
                return false;
            }

            db.GiveawayItems.Remove(giveawayItem);
            await db.SaveChangesAsync();
            _commandsLogger.Log.Information(
                "При обновлении розыгрыша {Id} произошла ошибка. Розыгрыш удалён из базы данных",
                giveawayItem.MessageId);

            return false;
        }

        await channel.ModifyMessageAsync(messageId, p => { p.Embed = form.Build(); }).ConfigureAwait(false);
        _commandsLogger.Log.Information("Розыгрыш в сообщении {Id} обновлён", giveawayItem.MessageId);

        giveawayItem.IsOver = true;
        await db.SaveChangesAsync();
        _commandsLogger.Log.Information("Розыгрыш {Id} завершён. Запись в базе данных обновлена",
            giveawayItem.MessageId);

        return true;
    }

    #endregion

    #region Notification

    public async Task CreateNotification()
    {
        var channel =
            (IMessageChannel)Client.GetChannel(ulong.Parse(_settings.CurrentValue.Discord.NotificationChannelId));

        var userInfo = _twitchApiService.GetUserByLogin(_settings.CurrentValue.Twitch.JoinChannel);

        if (!userInfo.isSuccess) return;

        (EmbedBuilder? embed, ComponentBuilder? button) form =
            await CreateNotificationForm(userInfo.user, NotificationType.Loading);

        var message =
            await channel.SendMessageAsync(embed: form.embed?.Build(), components: form.button?.Build());

        var retryCount = 0;
        (bool isSuccess, TwitchStreamInfo? streamInfo) streamInfo;

        do
        {
            streamInfo = _twitchApiService.GetStreamInfo(_settings.CurrentValue.Twitch.JoinChannel);

            if (streamInfo.isSuccess) continue;

            retryCount++;
            await Task.Delay(TimeSpan.FromSeconds(2));
        } while (retryCount < 30 && !streamInfo.isSuccess && StreamUp);

        if (!StreamUp)
        {
            await message.DeleteAsync();
            return;
        }

        try
        {
            form = streamInfo.isSuccess
                ? await CreateNotificationForm(userInfo.user, NotificationType.Full, streamInfo.streamInfo)
                : await CreateNotificationForm(userInfo.user, NotificationType.Default);
        }
        catch
        {
            form = await CreateNotificationForm(userInfo.user, NotificationType.Default);
        }

        await message.ModifyAsync(p =>
        {
            if (form.embed is null || form.button is null) return;

            p.Embed = form.embed.Build();
            p.Components = form.button.Build();
        });
    }

    private Task<(EmbedBuilder embed, ComponentBuilder button)> CreateNotificationForm(TwitchUser userInfo,
        NotificationType notificationType, TwitchStreamInfo? streamInfo = null)
    {
        var embed = new EmbedBuilder();
        var button = new ComponentBuilder();
        var url = $"https://twitch.tv/{_settings.CurrentValue.Twitch.JoinChannel}";

        switch (notificationType)
        {
            case NotificationType.Loading:
                embed.WithTitle("**Ожидание информации о стриме...**")
                    .WithDescription(
                        @$"**[{userInfo.DisplayName}]({url}) запустила стрим. Переходи по ссылке и присоединяйся!**")
                    .WithUrl(url)
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .WithFooter(footer =>
                    {
                        footer
                            .WithText($"{userInfo.DisplayName}");
                    })
                    .WithThumbnailUrl(userInfo.ProfileImage)
                    .WithImageUrl(NotificationLoadingGif)
                    .WithAuthor(author =>
                    {
                        author.WithName(userInfo.DisplayName)
                            .WithIconUrl(userInfo.ProfileImage);
                    })
                    .AddField("**Игра**", "Ожидание...", true)
                    .AddField("**Зрители**", "Ожидание...", true);

                button.WithButton("Вперёд на стрим!", style: ButtonStyle.Link, url: url);
                break;
            case NotificationType.Full:
                if (streamInfo != null)
                    embed.WithTitle($"**{streamInfo.Title}**")
                        .WithDescription(
                            $@"**[{userInfo.DisplayName}]({url}) запустила стрим. Переходи по ссылке и присоединяйся!**")
                        .WithUrl(url)
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp()
                        .WithFooter(footer => { footer.WithText(userInfo.DisplayName); })
                        .WithThumbnailUrl(userInfo.ProfileImage)
                        .WithImageUrl(streamInfo.ThumbnailImage)
                        .WithAuthor(author =>
                        {
                            author.WithName(userInfo.DisplayName);
                            author.WithIconUrl(userInfo.ProfileImage);
                        })
                        .AddField("**Игра**", $"{streamInfo.GameName}", true)
                        .AddField("**Зрители**", $"{streamInfo.ViewersCount}", true);

                button.WithButton("Вперёд на стрим!", style: ButtonStyle.Link, url: url);
                break;
            case NotificationType.Default:
                embed.WithTitle("**Стрим уже начался!**")
                    .WithDescription(
                        $@"**[{userInfo.DisplayName}]({url}) запустила стрим. Переходи по ссылке и присоединяйся!**")
                    .WithUrl(url)
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .WithFooter(footer => { footer.WithText(userInfo.DisplayName); })
                    .WithThumbnailUrl(userInfo.ProfileImage)
                    .WithImageUrl(NotificationDefaultGif)
                    .WithAuthor(author =>
                    {
                        author.WithName(userInfo.DisplayName);
                        author.WithIconUrl(userInfo.ProfileImage);
                    });

                button.WithButton("Вперёд на стрим!", style: ButtonStyle.Link, url: url);
                break;
        }

        return Task.FromResult((embed, button));
    }

    #endregion
}