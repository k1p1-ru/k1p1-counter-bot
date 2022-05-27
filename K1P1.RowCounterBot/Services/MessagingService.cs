using System.Text;
using System.Text.RegularExpressions;
using K1P1.RowCounterBot.Database;
using K1P1.RowCounterBot.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace K1P1.RowCounterBot.Services;

public class MessagingService
{
    public async Task ProcessMessage(
        long chatId,
        string message,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        if (message.StartsWith('/'))
        {
            switch (message)
            {
                case "/start":
                    await SendOnboardingMessage(chatId, botClient, ct);
                    return;
                case "/new":
                    await CreateCounterMessage(chatId, botClient, ct);
                    return;
                case "/counters":
                    await GetCountersMessage(Constants.SelectCommand, chatId, botClient, ct);
                    return;
                case "/archive":
                    await GetCountersMessage(Constants.ArchiveCommand, chatId, botClient, ct);
                    return;
                case "/archived":
                    await GetArchivedMessage(chatId, botClient, ct);
                    return;
                default:
                    await SendUnknownMessage(chatId, botClient, ct);
                    return;
            }
        }
        
        await ProcessTextMessage(chatId, message, botClient, ct);
    }

    private async Task ProcessTextMessage(
        long chatId,
        string message,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        await using var dbContext = new DefaultDbContext();
        {
            var state = await dbContext.StateMachines.FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
            if (state != null)
            {
                await ProcessStateMachineMessage(chatId, state, message, botClient, ct);
                return;
            }
        }

        await SendUnknownMessage(chatId, botClient, ct);
    }

    private async Task ProcessStateMachineMessage(
        long chatId,
        StateMachine stateMachine,
        string message,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        if (stateMachine.State == States.Adding && !string.IsNullOrWhiteSpace(message))
        {
            await using var dbContext = new DefaultDbContext();
            {
                var newId = await dbContext.Counters
                        .Where(x => x.ChatId == chatId)
                        .OrderByDescending(x => x.Id)
                        .Select(x => x.Id)
                        .FirstOrDefaultAsync(ct) + 1;

                dbContext.StateMachines.Remove(stateMachine);
                var counter = new Counter(newId, chatId, message);
                dbContext.Add(counter);
                await dbContext.SaveChangesAsync(ct);

                await SendCounterWithControlsMessage(chatId, null, counter, botClient, ct);

                return;
            }
        }

        await SendUnknownMessage(chatId, botClient, ct);
    }

    public async Task ProcessCallback(
        long chatId,
        int? inlineMessageId,
        string message,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        const string pattern = $"(?'task'{Constants.IncreaseCommand}|{Constants.DecreaseCommand}|{Constants.SelectCommand}|{Constants.ArchiveCommand}) (?'counter'\\d+)";
        if (!Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
        {
            await SendUnknownMessage(chatId, botClient, ct);
            return;
        }

        var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
        var counterId = int.Parse(match.Groups["counter"].Value);
        var task = match.Groups["task"].Value;
        await using var dbContext = new DefaultDbContext();
        {
            var counter = await dbContext.Counters
                .FirstOrDefaultAsync(x => x.ChatId == chatId && x.Id == counterId, ct);

            if (counter == null)
            {
                await SendUnknownMessage(chatId, botClient, ct);
                return;
            }

            switch (task)
            {
                case Constants.IncreaseCommand:
                    counter.Increase();
                    break;
                case Constants.DecreaseCommand:
                    counter.Decrease();
                    break;
                case Constants.SelectCommand:
                    await SendCounterWithControlsMessage(chatId, null, counter, botClient, ct);
                    return;
                case Constants.ArchiveCommand:
                    counter.Archive();
                    break;
                default:
                    await SendUnknownMessage(chatId, botClient, ct);
                    return;
            }

            await dbContext.SaveChangesAsync(ct);
            if (!counter.Archived)
                await SendCounterWithControlsMessage(chatId, inlineMessageId, counter, botClient, ct);
            else
                await GetCountersMessage(Constants.SelectCommand, chatId, botClient, ct);
        } 
    }

    private async Task SendCounterWithControlsMessage(
        long chatId,
        int? inlineMessageId,
        Counter counter,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("◀️", $"{Constants.DecreaseCommand} {counter.Id}"),
            InlineKeyboardButton.WithCallbackData("▶️", $"{Constants.IncreaseCommand} {counter.Id}"),
        });
        var message = $"🧶 *{counter.Name}* 🧶\n\nRows count: *{counter.RowCount}*";

        if (inlineMessageId == null)
        {
            await botClient.SendTextMessageAsync(chatId,
                message,
                ParseMode.MarkdownV2,
                replyMarkup: keyboard,
                cancellationToken: ct);
            return;
        }

        await botClient.EditMessageTextAsync(chatId, 
            inlineMessageId.Value,
            message,
            ParseMode.MarkdownV2,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    private async Task CreateCounterMessage(long chatId, ITelegramBotClient botClient, CancellationToken ct)
    {
        await using var dbContext = new DefaultDbContext();
        {
            if (await dbContext.Counters.CountAsync(ct) > Constants.MaxActiveCounters)
                await botClient.SendTextMessageAsync(chatId,
                    "We have no buttons left for your counters 🤷‍♂️! \n "
                    + "Archive some counters to free our buttons.",
                    replyMarkup: GetDefaultKeyboard(chatId),
                    cancellationToken: ct);

            await dbContext.Database.ExecuteSqlRawAsync("delete from StateMachines where chatId = @p0", chatId);
            dbContext.StateMachines.Add(new StateMachine(chatId, States.Adding));
            await dbContext.SaveChangesAsync(ct);
        }

        await botClient.SendTextMessageAsync(chatId,
            "Cool! How will you call it?",
            replyMarkup: GetDefaultKeyboard(chatId),
            cancellationToken: ct);
    }

    private async Task GetCountersMessage(string command, long chatId, ITelegramBotClient botClient, CancellationToken ct)
    {
        List<Counter> counters;
        await using var dbContext = new DefaultDbContext();
        {
            counters = await dbContext.Counters
                .Where(x => !x.Archived)
                .OrderByDescending(x => x.Id)
                .Take(Constants.MaxActiveCounters)
                .ToListAsync(ct);
        }

        if (!counters.Any())
        {
            await botClient.SendTextMessageAsync(chatId,
                "You have no active counters. 🤷‍♂️ Let's create a new one!",
                replyMarkup: GetDefaultKeyboard(chatId),
                cancellationToken: ct);
            return;
        }

        var keyboard = new InlineKeyboardMarkup(counters.Select(x =>
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{x.Name} ({x.RowCount} rows)",
                    $"{command} {x.Id}")
            }));

        await botClient.SendTextMessageAsync(chatId,
            "Here are your counters, pick one: ",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    private async Task GetArchivedMessage(long chatId, ITelegramBotClient botClient, CancellationToken ct)
    {
        List<Counter> counters;
        await using var dbContext = new DefaultDbContext();
        {
            counters = await dbContext.Counters
                .Where(x => x.Archived)
                .OrderByDescending(x => x.Id)
                .ToListAsync(ct);
        }
        
        if (!counters.Any())
        {
            await botClient.SendTextMessageAsync(chatId,
                "You have no archived counters. 🤷‍♂️ Let's create a new one!",
                replyMarkup: GetDefaultKeyboard(chatId),
                cancellationToken: ct);
            return;
        }

        var sb = new StringBuilder("Your archived counters 🗑:\n\n");
        foreach (var counter in counters)
        {
            sb.Append($"{counter.Name} ({counter.RowCount} rows)\n");
        }

        await botClient.SendTextMessageAsync(chatId,
            sb.ToString(),
            replyMarkup: GetDefaultKeyboard(chatId),
            cancellationToken: ct);
    }
    
    private static IReplyMarkup GetDefaultKeyboard(long chatId)
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new List<KeyboardButton>
            {
                "/new",
                "/counters",
            },
            new List<KeyboardButton>
            {
                "/archive",
                "/archived"
            }
        });
    }

    private static async Task SendUnknownMessage(
        long chatId,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        await botClient.SendTextMessageAsync(chatId,
            "Could not parse your message 🤷‍♂️",
            replyMarkup: GetDefaultKeyboard(chatId),
            cancellationToken: ct);
    }

    private async Task SendOnboardingMessage(
        long chatId,
        ITelegramBotClient botClient,
        CancellationToken ct)
    {
        await botClient.SendAnimationAsync(chatId,
            animation: new InputOnlineFile(
                new Uri("https://media.giphy.com/media/3oEhmHmWP3Y9wQxoli/giphy-downsized.gif")),
            cancellationToken: ct);
        await botClient.SendTextMessageAsync(chatId,
            "Now you can set your counters! 🧶",
            replyMarkup: GetDefaultKeyboard(chatId),
            cancellationToken: ct);
    }
}