using Microsoft.Extensions.Options;
using Towa.Settings;
using Towa.Twitch.Api.Logger.Interfaces;
using Towa.Twitch.Api.Models;
using Towa.Twitch.Api.Services.Interfaces;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Towa.Twitch.Api.Services;

public class TwitchApiService : ITwitchApiService
{
    private readonly ITwitchApiLogger _logger;
    private readonly IOptionsMonitor<TwitchModersList> _modersList;
    private readonly IOptionsMonitor<CoreSettings> _settings;

    public TwitchApiService(IOptionsMonitor<CoreSettings> settings, IOptionsMonitor<TwitchModersList> modersList,
        ITwitchApiLogger logger)
    {
        _settings = settings;
        _modersList = modersList;
        _logger = logger;
    }

    public (bool isSuccess, DateTime age) CheckUserFollowage(string userId, string channelId,
        DateTime? createdAt = null)
    {
        DateTime? followTime;

        if (createdAt is not null)
            followTime = createdAt;
        else
            followTime = GetTwitchApi().Helix.Users.GetUsersFollowsAsync(fromId: userId, toId: channelId).Result.Follows
                .FirstOrDefault()?
                .FollowedAt;

        if (followTime is null) return (false, DateTime.MinValue);

        var timeDifference = DateTime.UtcNow - followTime;
        var age = DateTime.MinValue + timeDifference;

        return (true, (DateTime)age);
    }

    public async Task<(bool isSuccess, string message)> BanUserReward(string broadcasterId, string login, string author,
        int duration = 300, string reason = "Награда за баллы канала")
    {
        var isModerator = CheckModerators(login);

        if (isModerator || login == _settings.CurrentValue.Twitch.JoinChannel.ToLower())
            return (false,
                "А стримера и модеров банить низяяя! etacarinaeRedrage Товики не верну, я вредный etacarinaeWondersip");

        var user = GetUserByLogin(login);

        if (!user.isSuccess)
        {
            _logger.Log.Error("Ошибка при получении Id зрителя для бана");
            return (false,
                "Мне не удалось найти зрителя, чтобы забанить etacarinaeBue Подождите немного и другой модератор исполнит вашу волю etacarinaeLOOTHalloween");
        }

        await GetTwitchApi().Helix.Moderation.BanUserAsync(broadcasterId, "526398713",
            new BanUserRequest
            {
                Duration = duration,
                UserId = user.user.Id,
                Reason = reason
            });

        _logger.Log.Information("Команда бана завершена успешно");

        return (true,
            $"отправляет {user.user.Login} немного отдохнуть на 5 минут etacarinaeSip");
    }

    public (bool isSuccess, TwitchUser user) GetUserByLogin(string login)
    {
        User[]? users = GetTwitchApi().Helix.Users.GetUsersAsync(logins: new List<string> { login }).Result.Users;

        if (users == null || users.Length == 0) return (false, new TwitchUser());

        var user = users.Select(user => new TwitchUser
        {
            Login = user.Login, DisplayName = user.DisplayName, Id = user.Id, ProfileImage = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt
        }).First();

        return (true, user);
    }

    public (bool isSuccess, TwitchUser user) GetUserById(string id)
    {
        User[]? users = GetTwitchApi().Helix.Users.GetUsersAsync(new List<string> { id }).Result.Users;

        if (users == null || users.Length == 0) return (false, new TwitchUser());

        var userList = users.Select(user => new TwitchUser
        {
            Login = user.Login, DisplayName = user.DisplayName, Id = user.Id, ProfileImage = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt
        }).First();

        return (true, userList);
    }

    public (bool isSuccess, TwitchStreamInfo? streamInfo) GetStreamInfo(string login)
    {
        Stream[]? streams = GetTwitchApi().Helix.Streams.GetStreamsAsync(userLogins: new List<string> { login }).Result
            .Streams;

        if (streams == null || streams.Length == 0) return (false, null);

        var stream = streams.Select(stream =>
                new TwitchStreamInfo(stream.Title, stream.GameName, stream.ViewerCount, stream.ThumbnailUrl))
            .First();

        var random = new Random();

        var randomNumber = random.Next(0, 999999999);

        stream.ThumbnailImage = stream.ThumbnailImage.Replace("{width}", "1280").Replace("{height}", "720") +
                                $"?r={randomNumber}";
        if (string.IsNullOrWhiteSpace(stream.Title)) stream.Title = "Нет названия стрима";
        if (string.IsNullOrWhiteSpace(stream.GameName)) stream.GameName = "Нет игры";

        return (true, stream);
    }

    public (bool isSuccess, TwitchChannelInfo? channelInfo) GetChannelInfo(string login)
    {
        var channels = GetTwitchApi().Helix.Channels.GetChannelInformationAsync(GetUserByLogin(login).user.Id).Result
            .Data;

        if (channels == null || channels.Length == 0) return (false, null);

        return (true, channels.Select(c => new TwitchChannelInfo(c.Title)).First());
    }

    public bool GetStreamStatus()
    {
        var stream = GetTwitchApi().Helix.Streams
            .GetStreamsAsync(userLogins: new List<string> { _settings.CurrentValue.Twitch.JoinChannel.ToLower() })
            .Result
            .Streams
            .FirstOrDefault();

        if (stream is not { Type: "live" }) return false;

        _logger.Log.Information("Прямая трансляция идёт");
        return true;
    }

    private bool CheckModerators(string login)
    {
        return _modersList.CurrentValue.ModersList.Any(m => m == login);
    }

    private TwitchAPI GetTwitchApi()
    {
        return new TwitchAPI
        {
            Settings =
            {
                ClientId = _settings.CurrentValue.Twitch.ClientId,
                AccessToken = _settings.CurrentValue.Twitch.OAuthKey
            }
        };
    }

    private static string ConvertToTimeCases(int totalSeconds)
    {
        var weeksCase = new[] { "неделя", "недели", "недель" };
        var daysCase = new[] { "день", "дня", "дней" };
        var hoursCase = new[] { "час", "часа", "часов" };
        var minutesCase = new[] { "минуту", "минуты", "минут" };
        var secondsCase = new[] { "секунду", "секунды", "секунд" };

        var weeksCount = totalSeconds / (7 * 24 * 60 * 60);
        var daysCount = totalSeconds / (7 * 24 * 60 * 60);
        var hoursCount = totalSeconds / (7 * 24 * 60 * 60);
        var minutesCount = totalSeconds / (7 * 24 * 60 * 60);
        var secondsCount = totalSeconds / (7 * 24 * 60 * 60);

        var weeks = FormatTimeElement(weeksCount, weeksCase);
        var days = FormatTimeElement(daysCount, daysCase);
        var hours = FormatTimeElement(hoursCount, hoursCase);
        var minutes = FormatTimeElement(minutesCount, minutesCase);
        var seconds = FormatTimeElement(secondsCount, secondsCase);

        var message = "";

        message += weeksCount > 0 ? $"{weeks} " : "";
        message += daysCount > 0 ? $"{days} " : "";
        message += hoursCount > 0 ? $"{hours} " : "";
        message += minutesCount > 0 ? $"{minutes} " : "";
        message += secondsCount > 0 ? $"{seconds} " : "";

        return message;
    }

    private static string FormatTimeElement(int value, string[] labels)
    {
        var cases = new[] { 2, 0, 1, 1, 1, 2 };
        var index = value % 100 / 10 == 1 ? 2 : cases[value % 10 < 5 ? value % 10 : 5];
        return $"{value} {labels[index]}";
    }
}