using Discord;
using Discord.WebSocket;
using Towa.Discord.Models;

namespace Towa.Discord.Handlers.Interfaces;

public interface IDiscordHandler
{
    public bool StreamUp { get; set; }
    public List<UpdaterItem> UpdaterItems { get; set; }
    public DiscordSocketClient Client { get; set; }
    public Task InitializeAsync();

    public Task<EmbedBuilder> CreateGiveawayForm(GiveawayItem giveawayItem,
        bool isEnded = false);

    public Task CheckUpdater(GiveawayItem giveawayItem);

    public Task<bool> EndGiveaway(ulong messageId);
    public Task CreateNotification();
}