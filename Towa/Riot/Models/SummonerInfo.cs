using Newtonsoft.Json;

namespace Towa.Riot.Models;

public class SummonerInfo
{
    public SummonerInfo(string id, string accountId, string name)
    {
        Id = id;
        AccountId = accountId;
        Name = name;
    }

    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("accountId")] public string AccountId { get; set; }

    [JsonProperty("name")] public string Name { get; set; }
}