using Towa.Twitch.Api.Models;

namespace Towa.Twitch.Api.Services.Interfaces;

public interface ITwitchApiService
{
    public (bool isSuccess, TwitchUser user) GetUserByLogin(string login);
    public (bool isSuccess, TwitchUser user) GetUserById(string id);

    public (bool isSuccess, DateTime age) CheckUserFollowage(string userId, string channelId,
        DateTime? createdAt = null);

    public (bool isSuccess, TwitchStreamInfo? streamInfo) GetStreamInfo(string login);

    public (bool isSuccess, TwitchChannelInfo? channelInfo) GetChannelInfo(string login);

    public Task<(bool isSuccess, string message)> BanUserReward(string broadcasterId, string login, string author,
        int duration = 300, string reason = "Награда за баллы канала");

    public bool GetStreamStatus();
}