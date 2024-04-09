using System.Globalization;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Towa.Discord.Database;
using Towa.Discord.Handlers.Interfaces;
using Towa.Discord.Logger.Interfaces;
using Towa.Discord.Modals;
using Towa.Discord.Models;
using Towa.Settings;

namespace Towa.Discord.Commands;

public class GiveawayCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IDbContextFactory<DiscordDbContext> _database;
    private readonly IDiscordHandler _handler;
    private readonly IDiscordCommandsLogger _logger;
    private readonly IOptionsMonitor<CoreSettings> _settings;

    public GiveawayCommand(IDbContextFactory<DiscordDbContext> database, IDiscordHandler handler,
        IOptionsMonitor<CoreSettings> settings,
        IDiscordCommandsLogger logger)
    {
        _database = database;
        _handler = handler;
        _settings = settings;
        _logger = logger;
    }

    #region End Giveaway

    [EnabledInDm(false)]
    [MessageCommand("Закончить розыгрыш")]
    public async Task CompleteGiveaway(IMessage message)
    {
        _logger.Log.Information("Получена команда на завершение розыгрыша {Id}", message.Id);
        await DeferAsync(true);

        var isSuccess = await _handler.EndGiveaway(message.Id).ConfigureAwait(false);

        if (isSuccess)
            _logger.Log.Information("Команда завершения розыгрыша завершена");
        else
            _logger.Log.Error("При выполнении команды завершения розыгрыша произошла ошибка");

        await FollowupAsync(!isSuccess
            ? $"{message.Author.Mention} Что-то пошло не так! Розыгрыш был закончен или он не хранится в базе данных"
            : "Розыгрыш завершён!", ephemeral: true, allowedMentions: AllowedMentions.All);
    }

    #endregion

    #region Create Giveaway

    [EnabledInDm(false)]
    [SlashCommand("розыгрыш", "Создать новый розыгрыш в текущем канале")]
    public async Task CallCreateGiveawayModal()
    {
        _logger.Log.Information("Получена команда создания розыгрыша");
        await RespondWithModalAsync<GiveawayModal>("giveaway");
        _logger.Log.Information("Форма создания розыгрыша отправлена");
    }

    [ModalInteraction("giveaway")]
    public async Task CreateGiveaway(GiveawayModal modal)
    {
        _logger.Log.Information("Получена форма для создания розыгрыша");

        ParseGiveawayEndTime(modal.GiveawayDuration, out var isParseTimeSuccess, out var parsedDateTime,
            out var timeErrorMessage);
        ParseGiveawayWinnersCount(modal.GiveawayWinnersCount, out var isParseWinnersCountSuccess,
            out var parsedWinnersCount, out var winnersErrorMessage);

        if (isParseTimeSuccess && isParseWinnersCountSuccess)
        {
            var item = new GiveawayItem
            {
                Title = modal.GiveawayTitle,
                WinnerCount = parsedWinnersCount,
                IsInfinite = parsedDateTime == DateTimeOffset.MinValue,
                Timestamp = parsedDateTime.ToUnixTimeSeconds(),
                Description = modal.GiveawayDescription,
                Image = modal.GiveawayImage,
                IsOver = false
            };
            _logger.Log.Information("Создана запись розыгрыша для базы данных");

            var form = await _handler.CreateGiveawayForm(item);
            _logger.Log.Information("Форма розыгрыша создана");

            var message = await ReplyAsync(embed: form.Build(), allowedMentions: AllowedMentions.All);
            _logger.Log.Information("Сообщение с розыгрышем отправлено");

            await message.AddReactionAsync(Emoji.Parse(_settings.CurrentValue.Discord.GiveawayEmote));
            _logger.Log.Information("Реакция для участия в розыгрыше добавлена к сообщению");

            item.MessageId = message.Id;
            item.ChannelId = message.Channel.Id;

            await using var db = await _database.CreateDbContextAsync();
            db.GiveawayItems.Add(item);
            await db.SaveChangesAsync();

            _logger.Log.Information("Запись розыгрыша помещена в базу данных");

            _ = _handler.CheckUpdater(item);
            _logger.Log.Information("Розыгрыш добавлен в систему отслеживания завершения розыгрышей");

            await RespondAsync("Розыгрыш успешно создан", ephemeral: true,
                allowedMentions: AllowedMentions.All);
            _logger.Log.Information("Создание розыгрыша завершено");
        }
        else
        {
            var errorList = new List<string>();

            if (!string.IsNullOrWhiteSpace(timeErrorMessage))
                errorList.Add(timeErrorMessage);

            if (!string.IsNullOrWhiteSpace(winnersErrorMessage))
                errorList.Add(winnersErrorMessage);

            var message = $"При создании розыгрыша возникли следующие ошибки:\n{string.Join("\n", errorList)}";
            await RespondAsync(message, ephemeral: true, allowedMentions: AllowedMentions.All);
            _logger.Log.Error("При создании розыгрыша возникли ошибки. В форме обнаружены следующие ошибки: {Errors}",
                errorList);
        }
    }

    #endregion

    #region Edit Giveaway

    [EnabledInDm(false)]
    [MessageCommand("Изменить розыгрыш")]
    public async Task ChangeGiveaway(IMessage message)
    {
        _logger.Log.Information("Получена команда изменения розыгрыша {Id}", message.Id);

        await using var db = await _database.CreateDbContextAsync();
        var giveawayitem = await db.GiveawayItems.FindAsync(message.Id);
        if (giveawayitem is null)
        {
            _logger.Log.Error("Розыгрыш {Id} не найден в базе данных", message.Id);
            goto End;
        }

        if (giveawayitem.IsOver)
        {
            _logger.Log.Error("Розыгрыш {Id} уже завершён и не может быть изменён", message.Id);
            goto End;
        }

        var modal = new ModalBuilder();
        modal.WithTitle("ИЗМЕНЕНИЕ РОЗЫГРЫША");
        modal.WithCustomId($"edit_giveaway:{giveawayitem.MessageId}");

        modal.AddTextInput("Название розыгрыша", "giveaway_title", required: true, value: giveawayitem.Title);
        modal.AddTextInput("Сколько победителей", "giveaway_winners", placeholder: "Число", required: true,
            value: giveawayitem.WinnerCount.ToString());
        modal.AddTextInput("Продолжительность розыгрыша", "giveaway_time",
            placeholder: "Пустое | 28.08.2022 16:00 | 15 минут/часов/дней/недель",
            value: giveawayitem.IsInfinite
                ? ""
                : DateTimeOffset.FromUnixTimeSeconds(giveawayitem.Timestamp).LocalDateTime
                    .AddHours(-_settings.CurrentValue.Discord.UtcHourCorrection).ToString("g"), required: false);
        modal.AddTextInput("Описание розыгрыша", "giveaway_description", TextInputStyle.Paragraph,
            value: giveawayitem.Description, required: false);
        modal.AddTextInput("Ссылка на картинку", "giveaway_image", placeholder: "https://", value: giveawayitem.Image,
            required: false);

        await RespondWithModalAsync(modal.Build());
        _logger.Log.Information("Отправлена форма для изменения розыгрыша {Id}", message.Id);
        return;

        End:

        await RespondAsync("Во время обновления произошла ошибка. Розыгрыш не найден в сообщении или он уже завершён",
            ephemeral: true);
    }

    [ModalInteraction("edit_giveaway:*")]
    public async Task EditGiveaway(ulong id, GiveawayModal modal)
    {
        _logger.Log.Information("Получена форма для изменения розыгрыша {Id}", id);
        await DeferAsync(true);

        string message;
        var isTitleChanged = false;
        var isWinnersChanged = false;
        var isTimestampChanged = false;
        var isUpdateNeeded = false;
        var isDescriptionChanged = false;
        var isImageChanged = false;

        await using var db = await _database.CreateDbContextAsync();
        var giveawayItem = await db.GiveawayItems.FindAsync(id);

        if (giveawayItem is null)
        {
            _logger.Log.Error("Розыгрыш {Id} не найден в базе данных", id);
            message = "У меня не получилось обновить розыгрыш( Не нашёл розыгрыш на моих кувшинках";
            goto End;
        }

        _logger.Log.Information("Розыгрыш {Id} найден. Проводится проверка данных формы", giveawayItem.MessageId);

        ParseGiveawayEndTime(modal.GiveawayDuration, out var isParseTimeSuccess, out var parsedDateTime,
            out var timeErrorMessage);
        ParseGiveawayWinnersCount(modal.GiveawayWinnersCount, out var isParseWinnersCountSuccess,
            out var parsedWinnersCount, out var winnersErrorMessage);

        if (isParseTimeSuccess && isParseWinnersCountSuccess)
        {
            if (modal.GiveawayTitle != giveawayItem.Title)
            {
                giveawayItem.Title = modal.GiveawayTitle;
                isTitleChanged = true;
                _logger.Log.Information("Название розыгрыша {Id} изменилось", giveawayItem.MessageId);
            }

            if (parsedWinnersCount != giveawayItem.WinnerCount)
            {
                giveawayItem.WinnerCount = parsedWinnersCount;
                isWinnersChanged = true;
                _logger.Log.Information("Количество победителей розыгрыша {Id} изменилось", giveawayItem.MessageId);
            }

            if (parsedDateTime.ToUnixTimeSeconds() != giveawayItem.Timestamp)
            {
                var updaterItem = _handler.UpdaterItems.Find(u => u.MessageId == giveawayItem.MessageId);

                if (updaterItem is not null)
                {
                    updaterItem.TokenSource.Cancel();
                    _handler.UpdaterItems.Remove(updaterItem);
                    _logger.Log.Information(
                        "Обнаружены изменения в дате завершения розыгрыша. Система обновления розыгрыша {Id} остановлена и удалена",
                        giveawayItem.MessageId);
                }

                if (parsedDateTime == DateTimeOffset.MinValue)
                {
                    giveawayItem.IsInfinite = true;
                    giveawayItem.Timestamp = parsedDateTime.ToUnixTimeSeconds();
                    _logger.Log.Information("Дата завершения розыгрыша {Id} изменилась и стала бесконечной",
                        giveawayItem.MessageId);
                }
                else
                {
                    giveawayItem.IsInfinite = false;
                    giveawayItem.Timestamp = parsedDateTime.ToUnixTimeSeconds();
                    isUpdateNeeded = true;
                    _logger.Log.Information("Дата завершения розыгрыша {Id} изменилась и была указана точная дата",
                        giveawayItem.MessageId);
                }

                isTimestampChanged = true;
            }

            if (modal.GiveawayDescription != giveawayItem.Description)
            {
                giveawayItem.Description = modal.GiveawayDescription;
                isDescriptionChanged = true;
                _logger.Log.Information("Описание розыгрыша {Id} изменилось", giveawayItem.MessageId);
            }

            if (modal.GiveawayImage != giveawayItem.Image)
            {
                giveawayItem.Image = modal.GiveawayImage;
                isImageChanged = true;
                _logger.Log.Information("Картинка розыгрыша {Id} изменилась", giveawayItem.MessageId);
            }

            if (!isTitleChanged && !isWinnersChanged && !isTimestampChanged && !isDescriptionChanged && !isImageChanged)
            {
                _logger.Log.Error("Невозможно обновить розыгрыш {Id}. Не обнаружены изменения в данных розыгрыша",
                    giveawayItem.MessageId);
                message = "Я не могу обновить розыгрыш. Ни один пункт розыгрыша не изменился";
                goto End;
            }

            var form = await _handler.CreateGiveawayForm(giveawayItem);
            _logger.Log.Information("Изменённая форма розыгрыша {Id} получена", giveawayItem.MessageId);

            var channel = _handler.Client.GetChannel(giveawayItem.ChannelId) as IMessageChannel;

            await channel!.ModifyMessageAsync(giveawayItem.MessageId, p => { p.Embed = form.Build(); });
            _logger.Log.Information("Сообщение с розыгрышем {Id} обновлено", giveawayItem.MessageId);

            await db.SaveChangesAsync();
            _logger.Log.Information("Розыгрыш {Id} обновлен в базе данных", giveawayItem.MessageId);

            if (isUpdateNeeded)
            {
                _ = _handler.CheckUpdater(giveawayItem);
                _logger.Log.Information("Система обновления розыгрыша {Id} запущена", giveawayItem.MessageId);
            }

            message = "Розыгрыш успешно обновлён";
            _logger.Log.Information("Обновление розыгрыша {Id} завершено", giveawayItem.MessageId);
        }
        else
        {
            var errorList = new List<string>();

            if (!string.IsNullOrWhiteSpace(timeErrorMessage))
                errorList.Add(timeErrorMessage);

            if (!string.IsNullOrWhiteSpace(winnersErrorMessage))
                errorList.Add(winnersErrorMessage);

            message =
                $"Я не могу обновить розыгрыш. Столкнулся со следующими ошибками:\n{string.Join("\n", errorList)}";
            _logger.Log.Error("При обновлении розыгрыша возникли ошибки. Обнаружены следующие ошибки: {Errors}",
                errorList);
        }

        End:

        await FollowupAsync(message, ephemeral: true, allowedMentions: AllowedMentions.All);
    }

    #endregion

    #region Support Methods

    private void ParseGiveawayEndTime(string timeString, out bool isSuccess, out DateTimeOffset timestamp,
        out string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(timeString))
        {
            var currentDate = DateTime.Now;

            if (DateTime.TryParseExact(timeString, "dd.MM.yyyy HH:mm", null, DateTimeStyles.AllowWhiteSpaces,
                    out var tempDate))
            {
                var correctTime = tempDate.AddHours(_settings.CurrentValue.Discord.UtcHourCorrection);
                timestamp = DateTime.Compare(correctTime, currentDate) > 0 ? correctTime : currentDate;
            }
            else
            {
                try
                {
                    var splitTime = timeString.Split(" ").ToList();

                    var duration = splitTime[0];
                    var type = splitTime[1];

                    if (int.TryParse(duration, out var number))
                    {
                        if (type.Contains("мину"))
                            timestamp = currentDate.AddMinutes(number);
                        else if (type.Contains("час"))
                            timestamp = currentDate.AddHours(number);
                        else if (type.Contains("дн") || type.Contains("ден"))
                            timestamp = currentDate.AddDays(number);
                        else if (type.Contains("неде"))
                            timestamp = currentDate.AddDays(number * 7);
                        else
                            timestamp = currentDate;
                    }
                    else
                    {
                        timestamp = currentDate;
                    }
                }
                catch
                {
                    timestamp = currentDate;
                }
            }

            if (timestamp != currentDate)
            {
                isSuccess = true;
                errorMessage = string.Empty;
            }
            else
            {
                isSuccess = false;
                errorMessage = "Неверный формат даты";
            }
        }
        else
        {
            isSuccess = true;
            timestamp = DateTimeOffset.MinValue;
            errorMessage = string.Empty;
        }
    }

    private static void ParseGiveawayWinnersCount(string winnersString, out bool isSuccess, out int parsedWinnersCount,
        out string errorMessage)
    {
        if (int.TryParse(winnersString, out var tempCount) && tempCount > 0)
        {
            isSuccess = true;
            parsedWinnersCount = tempCount;
            errorMessage = string.Empty;
        }
        else
        {
            isSuccess = false;
            parsedWinnersCount = 0;
            errorMessage = "Неверно указанно количество победителей";
        }
    }

    #endregion
}