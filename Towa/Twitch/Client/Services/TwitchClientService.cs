using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Towa.ChatGpt.Enums;
using Towa.ChatGpt.Services.Interfaces;
using Towa.Riot.Enums;
using Towa.Riot.Services.Interfaces;
using Towa.Settings;
using Towa.Twitch.Api.Services.Interfaces;
using Towa.Twitch.Client.Logger.Interfaces;
using Towa.Twitch.Client.Services.Interfaces;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace Towa.Twitch.Client.Services;

public class TwitchClientService : ITwitchClientService
{
    private readonly IChatGptService _chatGptService;
    private readonly ITwitchClientChatLogger _chatLogger;
    private readonly ConnectionCredentials _credentials;
    private readonly IOptionsMonitor<TwitchCustomCommands> _customCommands;
    private readonly ClientOptions _options;
    private readonly IRiotService _riotService;
    private readonly IOptionsMonitor<CoreSettings> _settings;
    private readonly ITwitchClientSystemLogger _systemLogger;
    private readonly ITwitchApiService _twApi;
    private TwitchClient _client = new();

    public TwitchClientService(IOptionsMonitor<CoreSettings> settings,
        IOptionsMonitor<TwitchCustomCommands> customCommands, ITwitchClientSystemLogger systemLogger,
        ITwitchClientChatLogger chatLogger, ITwitchApiService twApi, IChatGptService chatGptService,
        IRiotService riotService)
    {
        _settings = settings;
        _customCommands = customCommands;
        _systemLogger = systemLogger;
        _chatLogger = chatLogger;
        _twApi = twApi;
        _chatGptService = chatGptService;
        _riotService = riotService;

        _credentials = new ConnectionCredentials(_settings.CurrentValue.Twitch.BotName.ToLower(),
            _settings.CurrentValue.Twitch.OAuthKey);
        _options = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30),
            ReconnectionPolicy = new ReconnectionPolicy(3000, 1000),
            DisconnectWait = 10000
        };
    }

    public async Task StartTwitchClient()
    {
        _systemLogger.Log.Information("Запуск систем Твич клиента");

        WebSocketClient customClient = new(_options);
        _client = new TwitchClient(customClient);
        _client.Initialize(_credentials, _settings.CurrentValue.Twitch.JoinChannel.ToLower());

        _client.OnConnected += Connected;
        _client.OnJoinedChannel += JoinedChannel;
        _client.OnMessageReceived += MessageReceived;
        _client.OnChatCommandReceived += ChatCommandReceived;

        _systemLogger.Log.Information("Запуск систем Твич клиента завершена");

        bool isSuccess;

        do
        {
            isSuccess = _client.Connect();

            if (isSuccess) continue;

            _systemLogger.Log.Error("Не удалось подключиться к серверам Твич");
            await Task.Delay(2000);
        } while (!isSuccess);

        await Task.Delay(Timeout.Infinite);
    }

    public void SendMessage(string author, string message)
    {
        _client.SendMessage(_settings.CurrentValue.Twitch.JoinChannel.ToLower(), $"@{author} {message}");
    }

    private void ChatCommandReceived(object? sender, OnChatCommandReceivedArgs e)
    {
        if (!_settings.CurrentValue.Twitch.IsCommandsEnabled) return;

        _ = ProcessCommand(e);
    }

    private void MessageReceived(object? sender, OnMessageReceivedArgs message)
    {
        if (message.ChatMessage.Message.StartsWith("!")) return;

        _chatLogger.Log.Information("[{UserType}] {Username} : {Message}", message.ChatMessage.UserType,
            message.ChatMessage.Username, message.ChatMessage.Message);

        if (!message.ChatMessage.Message.ToLower().StartsWith($"@{_settings.CurrentValue.Twitch.BotName}")) return;

        _ = ProcessChatMessage(message);
    }

    private async Task ProcessChatMessage(OnMessageReceivedArgs message)
    {
        if (!_settings.CurrentValue.ChatGpt.IsActiveInTwitch) return;

        var cleanChatMessage =
            message.ChatMessage.Message.Replace($"@{_settings.CurrentValue.Twitch.BotName}", "",
                StringComparison.CurrentCultureIgnoreCase);

        cleanChatMessage = message.ChatMessage.EmoteSet.Emotes
            .Aggregate(cleanChatMessage, (current, emote) => current.Replace($"{emote.Name}", string.Empty))
            .TrimStart().TrimEnd();

        var response = await _chatGptService.GetChatMessage(ChatPlatform.Twitch,
            message.ChatMessage.UserId, message.ChatMessage.Username,
            cleanChatMessage,
            message.ChatMessage.IsBroadcaster);

        switch (response.reason)
        {
            case ResponseReason.Error:
            case ResponseReason.Stop:
                foreach (var responseMessage in response.messages)
                {
                    _client.SendMessage(_settings.CurrentValue.Twitch.JoinChannel,
                        $"@{message.ChatMessage.Username} {responseMessage}");

                    _chatLogger.Log.Information("[{UserType}] {Username} : {Message}", UserType.Moderator,
                        message.ChatMessage.BotUsername, responseMessage);

                    await Task.Delay(1000);
                }

                break;
            case ResponseReason.Length:
                foreach (var responseMessage in response.messages)
                {
                    _client.SendMessage(_settings.CurrentValue.Twitch.JoinChannel,
                        $"@{message.ChatMessage.Username} {responseMessage}");

                    _chatLogger.Log.Information("[{UserType}] {Username} : {Message}", UserType.Moderator,
                        message.ChatMessage.BotUsername, responseMessage);

                    await Task.Delay(1000);
                }

                var adittionalMessage =
                    $"@{message.ChatMessage.Username} У меня не влезло всё сообщение. Попроси меня продолжить отправив слово 'Продолжи'";

                _client.SendMessage(_settings.CurrentValue.Twitch.JoinChannel, adittionalMessage);

                _chatLogger.Log.Information("[{UserType}] {Username} : {Message}", UserType.Moderator,
                    message.ChatMessage.BotUsername, adittionalMessage);
                break;
        }
    }

    private void JoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        _systemLogger.Log.Information("Твич клиент подсоединился к каналу {Channel}",
            _settings.CurrentValue.Twitch.JoinChannel.ToLower());
    }

    private void Connected(object? sender, OnConnectedArgs e)
    {
        _systemLogger.Log.Information("Твич клиент подключён к серверам Твича");
    }

    private async Task ProcessCommand(OnChatCommandReceivedArgs command)
    {
        var chatMessage = command.Command.ChatMessage;
        var commandText = command.Command.CommandText.ToLower();

        _chatLogger.Log.Information("[{UserType}] {Username} : {Message}", chatMessage.UserType,
            chatMessage.Username, chatMessage.Message);

        var customCommand = _customCommands.CurrentValue.CustomCommands.FirstOrDefault(c =>
            c.ChatCommand == commandText || c.Alias.Contains(commandText));

        if (customCommand != null)
        {
            if (!customCommand.IsActive) return;
            if (!customCommand.IsForEveryone &&
                !(chatMessage.IsBroadcaster || chatMessage.IsModerator)) return;

            var customMessage = "@" + (command.Command.ArgumentsAsList.Count == 0
                ? $"{chatMessage.Username} {customCommand.Message}"
                : !Regex.IsMatch(command.Command.ArgumentsAsList[0].ToLower(), "^[а-яА-Я0-9_]+$")
                    ? $"{command.Command.ArgumentsAsList[0].ToLower().Replace("@", "")} {customCommand.Message}"
                    : $"{chatMessage.Username} {customCommand.Message}");

            _client.SendMessage(chatMessage.Channel, customMessage);

            _chatLogger.Log.Information("[{UserType}] {Username} : {Message}", UserType.Moderator,
                chatMessage.BotUsername, customMessage);

            return;
        }

        switch (commandText)
        {
            case "followage":
            case "age":
                var args = command.Command.ArgumentsAsList;
                var target = args.Count > 0 ? args[0].ToLower().Replace("@", "").Trim() : "";
                var isTargeted = !string.IsNullOrEmpty(target);
                var ageMessage = isTargeted && !Regex.IsMatch(target, "^[а-яА-Я0-9_]+$")
                    ? chatMessage.IsBroadcaster
                        ? GetStreamerFollowageMessage(chatMessage.RoomId, false)
                        : target.Contains(_settings.CurrentValue.Twitch.JoinChannel.ToLower())
                            ? GetStreamerFollowageMessage(chatMessage.RoomId, true)
                            : GetUserFollowageMessage(target, chatMessage.RoomId)
                    : GetSelfFollowageMessage(chatMessage.UserId, chatMessage.RoomId);

                var followageMessage = ageMessage.isSuccess
                    ? $"@{(isTargeted && !target.Contains(_settings.CurrentValue.Twitch.JoinChannel.ToLower()) ? $"{target} " : $"{chatMessage.Username} ")}{ageMessage.message}"
                    : $"@{chatMessage.Username} {ageMessage.message}";

                _client.SendMessage(chatMessage.Channel, followageMessage);

                _chatLogger.Log.Information("[{Moderator}] {ChatMessageBotUsername} : {FollowageMessage}",
                    UserType.Moderator, chatMessage.BotUsername, followageMessage);
                break;
            case "cleanchat":
            case "forget":
                var response = await _chatGptService.DeleteChat(chatMessage.Username);
                var forgetMessage = $"@{chatMessage.Username} {response}";

                _client.SendMessage(chatMessage.Channel, forgetMessage);

                _chatLogger.Log.Information("[{UserType}] {Username} : {Message}", UserType.Moderator,
                    chatMessage.BotUsername, forgetMessage);
                break;
            case "rank":
            case "ранг":
            case "эло":
            case "elo":
                _ = SendRankMessages(Region.All, command);
                break;
            case "rankru":
            case "рангру":
            case "элору":
            case "eloru":
                _ = SendRankMessages(Region.Ru, command);
                break;
            case "rankwest":
            case "rankeuw":
            case "вест":
            case "рангвест":
            case "рангеув":
            case "eloeuw":
                _ = SendRankMessages(Region.Euw, command);
                break;
        }
    }

    private (bool isSuccess, bool isTargeted, string message) GetSelfFollowageMessage(string userId, string channelId)
    {
        var followTime = _twApi.CheckUserFollowage(userId, channelId);
        return !followTime.isSuccess
            ? (false, false, "Я не нашёл тебя в нашём болоте 🐸🐸🐸")
            : (true, false, $"Ты в нашем болоте уже {Cases(followTime.age)} 🐸🐸🐸");
    }

    private (bool isSuccess, bool isTargeted, string message) GetStreamerFollowageMessage(string channelId,
        bool isToStreamer)
    {
        var userInfo = _twApi.GetUserById(channelId);
        if (!userInfo.isSuccess) return (false, false, "Я не нашёл тебя в нашём болоте 🐸🐸🐸");
        var followTime = _twApi.CheckUserFollowage("", "", userInfo.user.CreatedAt);
        return !followTime.isSuccess ? (false, false, "Я не нашёл тебя в нашём болоте 🐸🐸🐸") :
            isToStreamer ? (true, false, $"Нашему болоту уже {Cases(followTime.age)} 🐸🐸🐸") :
            (true, false, $"Твоему болоту уже {Cases(followTime.age)} 🐸🐸🐸");
    }

    private (bool isSuccess, bool isTargeted, string message) GetUserFollowageMessage(string login, string channelId)
    {
        var username = login.StartsWith("@") ? login.Replace("@", "") : login;
        var userInfo = _twApi.GetUserByLogin(username);
        if (!userInfo.isSuccess) return (false, true, "Я не нашёл этого зрителя в нашём болоте 🐸🐸🐸");
        var followTime = _twApi.CheckUserFollowage(userInfo.user.Id!, channelId);
        return followTime.isSuccess
            ? (true, true, $"<- Данная личность зафолловлена на наше болото уже {Cases(followTime.age)} 🐸🐸🐸")
            : (false, true, "Я не нашёл этого зрителя в нашём болоте 🐸🐸🐸");
    }

    private async Task SendRankMessages(Region region, OnChatCommandReceivedArgs command)
    {
        var leagueMessages = await _riotService.GetLeagueInfo(region);

        foreach (var leagueMessage in leagueMessages)
        {
            var rankMessage = "";
            var argsList = command.Command.ArgumentsAsList;

            if (argsList.Count == 0)
            {
                rankMessage += $@"{command.Command.ChatMessage.Username} {leagueMessage}";
            }
            else
            {
                var mention = argsList[0].ToLower().Replace("@", "");

                rankMessage += !Regex.IsMatch(mention, "^[а-яА-Я0-9_]+$")
                    ? $@"{mention} {leagueMessage}"
                    : $@"{command.Command.ChatMessage.Username} {leagueMessage}";
            }

            _client.SendMessage(command.Command.ChatMessage.Channel, rankMessage);

            _chatLogger.Log.Information("[{UserType}] {Username} : {Message}", UserType.Moderator,
                command.Command.ChatMessage.BotUsername, rankMessage);
        }
    }

    private static string Cases(DateTime dateTime)
    {
        var years = new[] { "год", "года", "лет" };
        var months = new[] { "месяц", "месяца", "месяцев" };
        var days = new[] { "день", "дня", "дней" };
        var hours = new[] { "час", "часа", "часов" };
        var minutes = new[] { "минуту", "минуты", "минут" };

        var year = dateTime.Year - 1;
        var month = dateTime.Month - 1;
        var day = dateTime.Day - 1;
        var hour = dateTime.Hour;
        var minute = dateTime.Minute;

        var responseYear = FormatTimeElement(year, years);
        var responseMonth = FormatTimeElement(month, months);
        var responseDay = FormatTimeElement(day, days);
        var responseHour = FormatTimeElement(hour, hours);
        var responseMinute = FormatTimeElement(minute, minutes);

        var message = "";

        message += year > 0 ? $"{responseYear} " : "";
        message += month > 0 ? $"{responseMonth} " : "";
        message += day > 0 ? $"{responseDay} " : "";
        message += hour > 0 ? $"{responseHour} " : "";
        message += minute > 0 ? $"{responseMinute} " : "";

        return message;
    }

    private static string FormatTimeElement(int value, IReadOnlyList<string> labels)
    {
        var cases = new[] { 2, 0, 1, 1, 1, 2 };
        var index = value % 100 / 10 == 1 ? 2 : cases[value % 10 < 5 ? value % 10 : 5];
        return $"{value} {labels[index]}";
    }
}