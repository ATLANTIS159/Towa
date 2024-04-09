using Towa.Riot.Enums;

namespace Towa.Riot.Services.Interfaces;

public interface IRiotService
{
    public Task<List<string>> GetLeagueInfo(Region region);
}