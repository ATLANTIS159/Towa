using Discord;
using Discord.Interactions;

namespace Towa.Discord.Modals;

public class GiveawayModal : IModal
{
    [InputLabel("Название розыгрыша")]
    [ModalTextInput("giveaway_title")]
    public string GiveawayTitle { get; set; } = "";

    [InputLabel("Сколько победителей")]
    [ModalTextInput("giveaway_winners", placeholder: "Число", initValue: "1")]
    public string GiveawayWinnersCount { get; set; } = "";

    [InputLabel("Продолжительность розыгрыша")]
    [ModalTextInput("giveaway_time", placeholder: "Пустое | 28.08.2022 16:00 | 15 минут/часов/дней/недель")]
    [RequiredInput(false)]
    public string GiveawayDuration { get; set; } = "";

    [InputLabel("Описание розыгрыша")]
    [ModalTextInput("giveaway_description", TextInputStyle.Paragraph)]
    [RequiredInput(false)]
    public string GiveawayDescription { get; set; } = "";

    [InputLabel("Ссылка на картинку")]
    [ModalTextInput("giveaway_image", placeholder: "https://")]
    [RequiredInput(false)]
    public string GiveawayImage { get; set; } = "";

    public string Title => "СОЗДАНИЕ НОВОГО РОЗЫГРЫША";
}