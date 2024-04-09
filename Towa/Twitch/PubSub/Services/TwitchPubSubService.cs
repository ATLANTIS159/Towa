using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Towa.Discord.Handlers.Interfaces;
using Towa.Settings;
using Towa.StreamDownloader.Services.Interfaces;
using Towa.Twitch.Api.Services.Interfaces;
using Towa.Twitch.Client.Services.Interfaces;
using Towa.Twitch.PubSub.Logger.Interfaces;
using Towa.Twitch.PubSub.Services.Interfaces;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace Towa.Twitch.PubSub.Services;

public class TwitchPubSubService : ITwitchPubSubService
{
    private readonly IDiscordHandler _discordHandler;
    private readonly ITwitchPubSubSystemLogger _pubSubSystemLogger;
    private readonly IOptionsMonitor<CoreSettings> _settings;
    private readonly IStreamDownloaderService _streamDownloaderService;
    private readonly ITwitchApiService _twitchApiService;
    private readonly ITwitchClientService _twitchClientService;
    private bool _isFirstStart = true;
    private TwitchPubSub? _pubSub;

    public TwitchPubSubService(IOptionsMonitor<CoreSettings> settings, IDiscordHandler discordHandler,
        ITwitchPubSubSystemLogger pubSubSystemLogger, ITwitchApiService twitchApiService,
        ITwitchClientService twitchClientService, IStreamDownloaderService streamDownloaderService)
    {
        _settings = settings;
        _discordHandler = discordHandler;
        _pubSubSystemLogger = pubSubSystemLogger;
        _twitchApiService = twitchApiService;
        _twitchClientService = twitchClientService;
        _streamDownloaderService = streamDownloaderService;
    }

    public Task StartPubSub()
    {
        _pubSubSystemLogger.Log.Information("Запуск систем Твич ПабСаб");

        _pubSub = new TwitchPubSub();

        _pubSub.OnStreamUp += OnStreamUp;
        _pubSub.OnStreamDown += OnStreamDown;
        _pubSub.OnPubSubServiceConnected += OnPubSubServiceConnected;
        _pubSub.OnRewardRedeemed += OnRewardRedeemed;

        _ = Updater();

        _pubSubSystemLogger.Log.Information("Запуск систем Твич ПабСаб завершена");
        return Task.CompletedTask;
    }

    private async Task Updater()
    {
        var user = _twitchApiService.GetUserByLogin(_settings.CurrentValue.Twitch.JoinChannel.ToLower());

        if (!user.isSuccess)
        {
            _pubSubSystemLogger.Log.Error(
                "При запуск систем Твич ПабСаб возникла ошибка. Проверьте название канала в настройках");
            return;
        }

        var id = user.user.Id;

        if (_pubSub != null)
        {
            _pubSub.ListenToVideoPlayback(id);
            _pubSub.ListenToRewards(id);
            _pubSub.Connect();

            var timer = new PeriodicTimer(TimeSpan.FromHours(1));

            while (await timer.WaitForNextTickAsync())
            {
                _pubSub.ListenToVideoPlayback(id);
                _pubSub.ListenToRewards(id);
                _pubSub.SendTopics(unlisten: true);

                await Task.Delay(1000);

                _pubSub.ListenToVideoPlayback(id);
                _pubSub.ListenToRewards(id);
                _pubSub.SendTopics();
            }
        }
    }

    private void OnPubSubServiceConnected(object? sender, EventArgs e)
    {
        _pubSub?.SendTopics();

        if (!_isFirstStart) return;

        _pubSubSystemLogger.Log.Information(
            "Система Твич ПабСаб подключилась к серварам и отслеживает канал {Channel}",
            _settings.CurrentValue.Twitch.JoinChannel.ToLower());
        if (_twitchApiService.GetStreamStatus()) StartStreamDownload();

        _isFirstStart = false;
    }

    private void OnStreamUp(object? sender, OnStreamUpArgs stream)
    {
        _pubSubSystemLogger.Log.Information(
            "Стрим на канале {Channel} начался", _settings.CurrentValue.Twitch.JoinChannel.ToLower());
        _discordHandler.StreamUp = true;
        if (_settings.CurrentValue.Discord.IsNotificationsActive) _ = _discordHandler.CreateNotification();
        StartStreamDownload();
    }

    private void OnStreamDown(object? sender, OnStreamDownArgs stream)
    {
        _pubSubSystemLogger.Log.Information(
            "Стрим на канале {Channel} закончился", _settings.CurrentValue.Twitch.JoinChannel.ToLower());
        _discordHandler.StreamUp = false;
    }

    private async void OnRewardRedeemed(object? sender, OnRewardRedeemedArgs reward)
    {
        if (reward.Status.ToLower() != "unfulfilled" || reward.RewardTitle != "BAN-Hammer") return;

        var author = reward.Login;
        var message = reward.Message.TrimEnd().TrimStart();

        if (string.IsNullOrWhiteSpace(message))
        {
            _twitchClientService.SendMessage(author.ToLower(),
                "Ты не указал кого хочешь забанить etacarinaeOmgpffff Неужели сам хочешь отдохнуть? etacarinaeSip");
            return;
        }

        var splitedMessage = RemoveChars(message).Split(' ').ToList();
        var target = splitedMessage.Where(word =>
                word.StartsWith('@') || !Regex.IsMatch(word, "^[а-яА-Я0-9_]+$") ||
                !Regex.IsMatch(word, "^[A-Za-z_0-9]+$") || !string.IsNullOrWhiteSpace(word))
            .ToList();

        if (target.Count > 1)
        {
            _twitchClientService.SendMessage(author.ToLower(),
                "Я запутался и не знаю кого банить etacarinaeCry Модер сейчас тебе поможет и исполнит твою волю etacarinaeBan");
            return;
        }

        var user = target.First();

        var login = user.StartsWith('@') ? user.Remove(0, 1) : user;

        var banStatus = await _twitchApiService.BanUserReward(reward.ChannelId, login.ToLower(), author.ToLower());

        if (!banStatus.isSuccess)
        {
            _twitchClientService.SendMessage(author.ToLower(), banStatus.message);
            return;
        }

        _twitchClientService.SendMessage(author.ToLower(), banStatus.message);
    }

    private static string RemoveChars(string message)
    {
        var charsToRemove = new List<char>
        {
            '#', '!', ',', '.', '$', '%', '^', '&', '*', '(', ')', ':', '"', '?', '>', '<', '[', ']', '{', '}', ';'
        };

        return charsToRemove.Aggregate(message, (current, c) => current.Replace(c.ToString(), string.Empty));
    }

    private void StartStreamDownload()
    {
        if (_settings.CurrentValue.StreamDownloaderSettings.IsDownloaderActive)
            _ = _streamDownloaderService.StartDownload();
    }
}