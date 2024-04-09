using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Towa.Riot.Enums;

namespace Towa.Settings;

public class RiotSettings
{
    public string ApiKey { get; set; } = "Введите Api ключ Riot";
    public List<RiotAccount> RiotAccounts { get; set; } = new();
}

public class RiotAccount
{
    public bool IsActive { get; set; } = false;
    public bool ShowInGlobalCommand { get; set; } = false;

    [JsonConverter(typeof(StringEnumConverter))]
    public Region Region { get; set; } = Region.Ru;

    public string PuuId { get; set; } = "Введите уникальный PuuID аккаунта Riot";

    public string AdditionalMessage { get; set; } =
        "Дополнительная информация в конце. Удалить текст и оставить пустым если не требуется";
}