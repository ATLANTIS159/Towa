using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Towa.Riot.Enums;
using Towa.Riot.Logger.Interfaces;
using Towa.Riot.Models;
using Towa.Riot.Services.Interfaces;
using Towa.Settings;

namespace Towa.Riot.Services;

public class RiotService : IRiotService
{
    private const string LeagueInfoEndpoint = "{0}/lol/league/v4/entries/by-summoner/{1}";
    private const string SummonerInfoEndpoint = "{0}/lol/summoner/v4/summoners/by-puuid/{1}";
    private readonly IRiotLogger _logger;
    private readonly IOptionsMonitor<CoreSettings> _settings;

    public RiotService(IOptionsMonitor<CoreSettings> settings, IRiotLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<string>> GetLeagueInfo(Region region)
    {
        var accounts = region == Region.All
            ? _settings.CurrentValue.Riot.RiotAccounts
                .Where(account => account is { IsActive: true, ShowInGlobalCommand: true }).ToList()
            : _settings.CurrentValue.Riot.RiotAccounts.Where(account => account.IsActive && account.Region == region)
                .ToList();

        if (accounts.Count == 0)
        {
            _logger.Log.Warning($"Не найдены аккаунты с запрашиваемым регионом. Регион: {region}");
            return new List<string>
            {
                region == Region.All
                    ? "Я не нашёл аккаунтов ни на одном сервере etacarinaeXMasCRY"
                    : $"Я не нашёл аккаунтов на {region} сервере etacarinaeXMasCRY"
            };
        }

        var messagesList = new List<string>();
        var client = GetHttpClient();

        foreach (var account in accounts)
        {
            var summonerRequest = new HttpRequestMessage(HttpMethod.Get,
                string.Format(SummonerInfoEndpoint, GetRiotServerAddress(account.Region), account.PuuId));
            var summonerResponse = await client.SendAsync(summonerRequest);

            if (!summonerResponse.IsSuccessStatusCode)
            {
                _logger.Log.Error(
                    $"Произошла ошибка при получении данных об аккаунте. Сообщение ошибки: {summonerResponse.ReasonPhrase}");
                return new List<string>
                {
                    "Я не смог получить данных об аккаунте etacarinaeXMasCRY Попросите чатик помочь вам etacarinaeXMasLOVE"
                };
            }

            var summonerInfo =
                JsonConvert.DeserializeObject<SummonerInfo>(await summonerResponse.Content.ReadAsStringAsync());

            if (summonerInfo is null)
            {
                _logger.Log.Error("Произошла ошибка при десериализации данных об аккаунте");
                return new List<string>
                {
                    "Я не смог получить данных об аккаунте etacarinaeXMasCRY Попросите чатик помочь вам etacarinaeXMasLOVE"
                };
            }

            var rankRequest = new HttpRequestMessage(HttpMethod.Get,
                string.Format(LeagueInfoEndpoint, GetRiotServerAddress(account.Region), summonerInfo.Id));
            var rankResponse = await client.SendAsync(rankRequest);

            if (!rankResponse.IsSuccessStatusCode)
            {
                _logger.Log.Error(
                    $"Произошла ошибка при получении данных о ранге аккаунта {account.PuuId} и ником {summonerInfo.Name}. Сообщение ошибки: {rankResponse.ReasonPhrase}");
                return new List<string>
                {
                    "Я не смог получить данных о ранге аккаунта etacarinaeXMasCRY Попросите чатик помочь вам etacarinaeXMasLOVE"
                };
            }

            var rankInfo =
                JsonConvert.DeserializeObject<List<RankInfo>>(await rankResponse.Content
                    .ReadAsStringAsync())?.FirstOrDefault(l => l.QueueType == "RANKED_SOLO_5x5");

            if (rankInfo is null)
            {
                messagesList.Add(GenerateRankMessage(account, summonerInfo));
                continue;
            }

            messagesList.Add(GenerateRankMessage(account, summonerInfo, rankInfo));
        }

        return messagesList;
    }

    private HttpClient GetHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-Riot-Token", _settings.CurrentValue.Riot.ApiKey);

        return client;
    }

    private static string GetRiotServerAddress(Region region)
    {
        return region switch
        {
            Region.Euw => "https://euw1.api.riotgames.com",
            Region.Ru => "https://ru.api.riotgames.com",
            Region.All => "",
            _ => ""
        };
    }

    private static string GenerateRankMessage(RiotAccount account, SummonerInfo summoner, RankInfo? rank = null)
    {
        var message = $"{account.Region.ToString().ToUpper()} Server -> Nickname: {summoner.Name}";

        message += rank is null
            ? " || Rank: Пока нет ранга"
            : $" || Rank: {rank.Tier} {rank.Rank} || Rank Points: {rank.LeaguePoints}";

        message += string.IsNullOrWhiteSpace(account.AdditionalMessage) ? "" : $" || {account.AdditionalMessage}";

        return message;
    }
}