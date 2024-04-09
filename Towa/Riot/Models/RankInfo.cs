using Newtonsoft.Json;

namespace Towa.Riot.Models;

public class RankInfo
{
    public RankInfo(string queueType, string tier, string rank, string summonerName, int leaguePoints)
    {
        QueueType = queueType;
        Tier = tier;
        Rank = rank;
        SummonerName = summonerName;
        LeaguePoints = leaguePoints;
    }

    [JsonProperty("queueType")] public string QueueType { get; set; }

    [JsonProperty("tier")] public string Tier { get; set; }

    [JsonProperty("rank")] public string Rank { get; set; }

    [JsonProperty("summonerName")] public string SummonerName { get; set; }

    [JsonProperty("leaguePoints")] public int LeaguePoints { get; set; }
}