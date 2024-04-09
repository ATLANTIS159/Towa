using Towa.ChatGpt.Services.Interfaces;
using Towa.Discord.Services.Interfaces;
using Towa.Twitch.Api.Services.Interfaces;
using Towa.Twitch.Client.Services.Interfaces;
using Towa.Twitch.PubSub.Services.Interfaces;

namespace Towa;

public class Core
{
    private readonly IServiceProvider _provider;

    public Core(IServiceProvider provider)
    {
        _provider = provider;
    }

    public void InitCore()
    {
        _ = _provider.GetRequiredService<IChatGptService>().Init();
        _ = _provider.GetRequiredService<ITwitchClientService>().StartTwitchClient();
        _ = _provider.GetRequiredService<ITwitchPubSubService>().StartPubSub();
        _ = _provider.GetRequiredService<IDiscordService>().StartService();
        _provider.GetRequiredService<ITwitchApiService>().GetStreamStatus();
    }
}