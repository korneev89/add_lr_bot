using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using MihaZupan;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TlgrmBot
{
	class Program
	{
		static ITelegramBotClient botClient;
		static readonly string[] Scopes = { CalendarService.Scope.CalendarEvents };
		static string ApplicationName = "Google Calendar API - DKOR TLGRM BOT";
		static CalendarService service;
		static readonly string calendarID = ConfigurationManager.AppSettings["calendarID"];
		static readonly string token = ConfigurationManager.AppSettings["token"];
        static readonly string botLaunchTimeUTC = string.Format("{0:[HH:mm:ss] dd.MM.yyyy}", DateTime.UtcNow);
        static readonly Dictionary<int, string> users = File.ReadLines(Helper.FullPathToFile(@"users.csv")).Select(line => line.Split(';')).ToDictionary(line => int.Parse(line[0]), line => line[1]);
        static readonly Dictionary<long, string> starts = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => line[1]);
        static readonly Dictionary<long, LeaveRequestEvent> leaveRequestEvents = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => new LeaveRequestEvent());
        static readonly Dictionary<long, SickLeaveEvent> sickLeaveEvents = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => new SickLeaveEvent());

        static void Main()
		{
            //var proxy = new HttpToSocks5Proxy("51.144.50.115", 1488, "sockduser", "SerejaTigr");
            //botClient = new TelegramBotClient(token, proxy);

            botClient = new TelegramBotClient(token);
			UserCredential credential;

			using (var stream =
				new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
			{
				string credPath = "token.json";
				credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
					GoogleClientSecrets.Load(stream).Secrets,
					Scopes,
					"user",
					CancellationToken.None,
					new FileDataStore(credPath, true)).Result;
				Console.WriteLine("Credential file saved to: " + credPath);
			}

			service = new CalendarService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName
			});

			var me = botClient.GetMeAsync().Result;
			Console.WriteLine(
			  $"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Hello, World! I am user {me.Id} and my name is {me.FirstName}."
			);

            string str = Dns.GetHostAddresses(Environment.MachineName).Last().ToString();
            botClient.SendTextMessageAsync(users.First().Key, "Чувак, я жив!\nHostame: *" + Environment.MachineName + "*\nIP: " + str, ParseMode.Markdown);

            botClient.OnMessage += Bot_OnMessage;
			botClient.OnCallbackQuery += Bot_OnCallback;
            botClient.StartReceiving();
			Thread.Sleep(int.MaxValue);
		}
		static async void Bot_OnMessage(object sender, MessageEventArgs e)
		{
			if ( e.Message.Text != null && users.TryGetValue(e.Message.From.Id, out string value))
			{
				Console.WriteLine( $"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Received a text message \"{e.Message.Text}\" in chat with {users[e.Message.From.Id]}.");
                LeaveRequestEvent.Clear(leaveRequestEvents[e.Message.From.Id]);

                List<InlineKeyboardButton> cancelButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                };

                List<InlineKeyboardButton> cancelDaysButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_days")
                };

                List<InlineKeyboardButton> cancelSickButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_sick")
                };

                List<InlineKeyboardButton> cancelDeletionButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_deletion")
                };

                List<InlineKeyboardButton> okShowButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("✅ ОK", "ok_show")
                };

                List<InlineKeyboardButton> okDeletionButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("✅ ОK", "ok_deletion")
                };

                List<InlineKeyboardButton> okHostButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("✅ ОK", "ok_host")
                };

                switch (e.Message.Text)
				{
					case "/help":
					case "/?":
                       await botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: "Утром лицо застряло в текстурах подушки? Бот добавит/удалит событие в календаре [[SED]] *\"Leave Requests\"*" +
                            "\n\n/start \"Диме плохо\" или добавить leave request" +
                            "\n/sick \"Диме *очень* плохо\" или добавить sick leave" +
                            "\n/del если выбрался из текстур и нужно удалить событие" +
                            "\n/show те кто не смог сегодня или не сможет завтра",
                            parseMode: ParseMode.Markdown
                        );
                        break;

                    case "/host":
                        string ip = Dns.GetHostAddresses(Environment.MachineName).Last().ToString();
                        List<List<InlineKeyboardButton>> hostKeyboard = new List<List<InlineKeyboardButton>>
                        {
                            okHostButtonRow
                        };


                        await botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: "Hostame: *" + Environment.MachineName + " *\nIP: " + ip + "\nStarted [UTC]: " + botLaunchTimeUTC,
                            parseMode: ParseMode.Markdown,
                            replyMarkup: new InlineKeyboardMarkup(hostKeyboard)
                        );
                        break;

                    case "/del":

                        List<List<InlineKeyboardButton>> deletionKeyboard = new List<List<InlineKeyboardButton>>();
                        string txt = "Нет событий для удаления...";

                        var leaveRequests = service.Events.List(calendarID);
                        leaveRequests.TimeMin = DateTime.UtcNow.Date;
                        IList<Event> allLeaveRequest = leaveRequests.Execute().Items;

                        if (allLeaveRequest.Count > 0)
                        {
                            IList<Event> userLeaveRequests = allLeaveRequest.Where(ev => ev.Creator?.Email == "dmitri.korneev@cbsinteractive.com"
                              && ev.Summary == $"{users[e.Message.From.Id]}'s leave request")
                              .OrderBy(ev => ev.Start.DateTime)
                              .ThenBy(ev => ev.End.DateTime)
                              .ToList();

                            if (userLeaveRequests.Count > 0)
                            {
                                txt = "Выбери событие которое нужно удалить:";

                                foreach (Event ev in userLeaveRequests)
                                {
                                    string day;
                                    int delta = 3 - (DateTime.Now.Hour - DateTime.UtcNow.Hour); // 3 hour - for MSC time shift

                                    if (ev.Start.DateTime.Value.Date == DateTime.UtcNow.AddHours(3).Date)
                                    {
                                        day = "сегодня";
                                    }
                                    else if (ev.Start.DateTime.Value.Date == DateTime.UtcNow.AddHours(3).AddDays(1).Date)
                                    {
                                        day = "завтра";
                                    }
                                    else
                                    {
                                        day = string.Format("{0:dd/MM}", ev.Start.DateTime);
                                    }

                                    List<InlineKeyboardButton> buttonRow = new List<InlineKeyboardButton>
                                    {
                                        InlineKeyboardButton.WithCallbackData($"[LR] {day} | {string.Format("{0:HH:mm}", ev.Start.DateTime.Value.AddHours(delta))} - {string.Format("{0:HH:mm}", ev.End.DateTime.Value.AddHours(delta))}", $"delete{ev.Id}")
                                    };

                                    deletionKeyboard.Add(buttonRow);
                                }
                            }
                        }

                        var sickLeaves = service.Events.List(calendarID);
                        sickLeaves.TimeMin = DateTime.UtcNow.Date.AddDays(-14);
                        IList<Event> allSickLeaves = sickLeaves.Execute().Items;

                        if (allSickLeaves.Count > 0)
                        {
                            IList<Event> userSickLeaves = allSickLeaves.Where(ev => ev.Creator?.Email == "dmitri.korneev@cbsinteractive.com"
                                && ev.Summary == $"{users[e.Message.From.Id]}'s sick leave")
                                .OrderBy(ev => ev.Start.Date)
                                .ThenBy(ev => ev.End.Date)
                                .ToList();

                            if (userSickLeaves.Count > 0)
                            {
                                foreach (Event ev in userSickLeaves)
                                {
                                    List<InlineKeyboardButton> buttonRow = new List<InlineKeyboardButton>
                                        {
                                            InlineKeyboardButton.WithCallbackData($"[SL] {ev.Start.Date} >>> {ev.End.Date}", $"delete{ev.Id}")
                                        };

                                    deletionKeyboard.Add(buttonRow);
                                }
                            }
                        }

                        if (deletionKeyboard.Count > 0)
                        {
                            txt = "Выбери событие которое нужно удалить:";
                            deletionKeyboard.Add(cancelDeletionButtonRow);
                        }
                        else
                        {
                            deletionKeyboard.Add(okDeletionButtonRow);
                        }
                          
                        await botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: txt,
                            replyMarkup: new InlineKeyboardMarkup(deletionKeyboard),
                            parseMode: ParseMode.Markdown
                        );
                        break;

                    case "/show":
                        var req = service.Events.List(calendarID);
                        req.TimeMin = DateTime.UtcNow.Date;
                        IList<Event> events = req.Execute().Items;

                        IList<Event> evs = events.Where(ev => ev.Creator?.Email == "dmitri.korneev@cbsinteractive.com"
                            && ev.Description == "Event created using telegram bot").
                            OrderBy(ev => ev.Start.DateTime).
                            ThenBy(ev => ev.End.DateTime).
                            ThenBy(ev => ev.Start.Date).
                            ThenBy(ev => ev.End.Date).
                            ThenBy(ev => ev.Summary).
                            ToList();

                        var msg = "На ближайшее время нет созданных событий 🤷‍♂️";

                        if (evs.Count > 0)
                        {

                            msg = "Ближайшие события в календаре:\n";
                            foreach (Event ev in evs)
                            {
                                string day;
                                int delta = 3 - (DateTime.Now.Hour - DateTime.UtcNow.Hour); // 3 hour - for MSC time shift

                                if (ev.Start.DateTime != null && ev.End.DateTime != null)
                                {
                                    if (string.Format("{0:dd/MM/yy}", ev.Start.DateTime) == string.Format("{0:dd/MM/yy}", DateTime.UtcNow.AddHours(3))) { day = "сегодня"; }
                                    else if (string.Format("{0:dd/MM/yy}", ev.Start.DateTime) == string.Format("{0:dd/MM/yy}", DateTime.UtcNow.AddHours(3).AddDays(1))) { day = "завтра"; }
                                    else { day = string.Format("{0:dd/MM}", ev.Start.DateTime); }
                                    msg += $"\n[[LR]] {ev.Summary.Replace("'s leave request", string.Empty)} | {day} | {string.Format("{0:HH:mm}", ev.Start.DateTime.Value.AddHours(delta))} - {string.Format("{0:HH:mm}", ev.End.DateTime.Value.AddHours(delta))}";
                                }
                                else
                                {
                                    msg += $"\n[[SL]] {ev.Summary.Replace("'s sick leave", string.Empty)} |  {ev.Start.Date} >>> {ev.End.Date}";
                                }
                            }
                        }

                        List<List<InlineKeyboardButton>> showKeyboard = new List<List<InlineKeyboardButton>>
                        {
                            okShowButtonRow
                        };

                        await botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: msg,
                            replyMarkup: new InlineKeyboardMarkup(showKeyboard),
                            parseMode: ParseMode.Markdown
                        );
                        break;

                    case "/start":

                        List<InlineKeyboardButton> daysButtonRow = new List<InlineKeyboardButton>
                            {
								InlineKeyboardButton.WithCallbackData("сегодня", "today"),
								InlineKeyboardButton.WithCallbackData("завтра", "tomorrow")
							};

                        List<List<InlineKeyboardButton>> daysKeyboard = new List<List<InlineKeyboardButton>>
							{
								daysButtonRow,
								cancelDaysButtonRow
							};

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Когда поставить leave request?",
							replyMarkup: new InlineKeyboardMarkup(daysKeyboard)
							);
						break;

                    case "/sick":

                        List<List<InlineKeyboardButton>> sickKeyboard = CreateCalendarKeyboard(DateTime.UtcNow.AddHours(3), "sick_start");

                        await botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: "С какого дня ты болеешь?", //Когда поставить сыкливого (sick leave)?",
                            replyMarkup: new InlineKeyboardMarkup(sickKeyboard)
                            );
                        break;

                    default:
                        await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Я не знаю такой команды, смотри /help"
						);
						break;
				}
			}
			if (!users.TryGetValue(e.Message.From.Id, out string value_1))
			{
				Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Received a text message from unauthorized user with id {e.Message.From.ToString()}.");
                await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: $"Дружок, ты не авторизован! Попроси [Димана](tg://user?id=168694373), чтобы добавил тебя. Твой id {e.Message.From.Id}",
							parseMode: ParseMode.Markdown
						);
			}
		}

        private static List<List<InlineKeyboardButton>> CreateCalendarKeyboard(DateTime date, string target)
        {
            string year = date.Year.ToString();
            string month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.Month);
            DateTime firstDay = new DateTime(date.Year, date.Month, 1);
            int daysInCurrentMonth = DateTime.DaysInMonth(date.Year, date.Month);
            DateTime lastDay = new DateTime(date.Year, date.Month, daysInCurrentMonth);

            int dayOfWeekFirst = ((int)firstDay.DayOfWeek > 0) ? (int)firstDay.DayOfWeek : 7;
            int dayOfWeekLast = ((int)lastDay.DayOfWeek > 0) ? (int)lastDay.DayOfWeek : 7;
            int rowsCount = (dayOfWeekFirst - 1 + daysInCurrentMonth + (7 - dayOfWeekLast))/7;

            List<InlineKeyboardButton> dateRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData($"{month} {year}", "calendar_dateRow")
                };

            List<InlineKeyboardButton> daysOfTheWeekRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("·пн·", "пн"),
                    InlineKeyboardButton.WithCallbackData("·вт·", "вт"),
                    InlineKeyboardButton.WithCallbackData("·ср·", "ср"),
                    InlineKeyboardButton.WithCallbackData("·чт·", "чт"),
                    InlineKeyboardButton.WithCallbackData("·пт·", "пт"),
                    InlineKeyboardButton.WithCallbackData("·сб·", "сб"),
                    InlineKeyboardButton.WithCallbackData("·вс·", "вс")
                };

            List<InlineKeyboardButton> calendarNavigationRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData($"< {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.AddMonths(-1).Month)}", $"calendar_update_{target}{firstDay.AddMonths(-1).Ticks}"),
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_calendar"),
                    InlineKeyboardButton.WithCallbackData($"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.AddMonths(1).Month)} >", $"calendar_update_{target}{firstDay.AddMonths(+1).Ticks}")
                };

            List<List<InlineKeyboardButton>> calendarKeyboard = new List<List<InlineKeyboardButton>>
            {
                dateRow,
                daysOfTheWeekRow
            };

            for (var row = 0; row < rowsCount; row++)
            {
                List<InlineKeyboardButton> daysRow = new List<InlineKeyboardButton>();
                for (var day = 1; day < 8; day++)
                {
                    string text;
                    string callback;
                    if ((row == 0 && day < dayOfWeekFirst) || (row == rowsCount - 1 && day > dayOfWeekLast))
                    {
                        text = "·";
                        callback = "calendar_empty";
                    }
                    else
                    {
                        int d = 7 * row + day - dayOfWeekFirst + 1;
                        text = d.ToString();
                        DateTime now = DateTime.UtcNow.AddHours(3);
                        DateTime resultDay = new DateTime(date.Year, date.Month, d, 0, 0, 0, DateTimeKind.Utc);

                        if (new DateTime(now.Year, now.Month, now.Day) == resultDay)
                        {
                            text = "- " + text + " -";
                        }
                        callback = $"{target}{resultDay.Ticks}";
                    }

                    InlineKeyboardButton dayButton = InlineKeyboardButton.WithCallbackData(text, callback);
                    daysRow.Add(dayButton);
                }
                calendarKeyboard.Add(daysRow);
            }

            calendarKeyboard.Add(calendarNavigationRow);

            return calendarKeyboard;
        }

        static async void Bot_OnCallback(object sender, CallbackQueryEventArgs e)
		{
			if (e.CallbackQuery.Data != null)
            {
                Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Received a callback query \"{e.CallbackQuery.Data}\" in chat with {users[e.CallbackQuery.From.Id]}.");

                switch (Regex.Replace(e.CallbackQuery.Data, @"[\d-]", string.Empty))
                {
                    case "today":
                    case "tomorrow":
                        await MessageDeletionOnCallback(e);

                        List<List<InlineKeyboardButton>> startHoursKeyboard = CreateStartHoursKeyboard(e.CallbackQuery.Message.Chat.Id);

                        await botClient.SendTextMessageAsync(
                            chatId: e.CallbackQuery.From.Id,
                            text: $"Со скольки?",
                            replyMarkup: new InlineKeyboardMarkup(startHoursKeyboard)
                        );
                        leaveRequestEvents[e.CallbackQuery.From.Id].Date = e.CallbackQuery.Data;
                        break;

                    case "calendar_update_sick_start":

                        await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);

                        long ticks = long.Parse(e.CallbackQuery.Data.Substring(26));
                        List<List<InlineKeyboardButton>> sickKeyboard = CreateCalendarKeyboard(new DateTime(ticks), "sick_start");

                        await botClient.EditMessageReplyMarkupAsync(
                            chatId: e.CallbackQuery.From.Id,
                            messageId: e.CallbackQuery.Message.MessageId,
                            replyMarkup: new InlineKeyboardMarkup(sickKeyboard)
                        );
                        break;

                    case "calendar_update_sick_end":

                        await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);

                        long endTicks = long.Parse(e.CallbackQuery.Data.Substring(24));
                        List<List<InlineKeyboardButton>> sickEndKeyboardUpdated = CreateCalendarKeyboard(new DateTime(endTicks), "sick_end");

                        await botClient.EditMessageReplyMarkupAsync(
                            chatId: e.CallbackQuery.From.Id,
                            messageId: e.CallbackQuery.Message.MessageId,
                            replyMarkup: new InlineKeyboardMarkup(sickEndKeyboardUpdated)
                        );
                        break;

                    case "sick_start":

                        await MessageDeletionOnCallback(e);
                        long sickStartTicks = long.Parse(e.CallbackQuery.Data.Substring(10));

                        sickLeaveEvents[e.CallbackQuery.From.Id].Start = new DateTime(sickStartTicks);

                        List<List<InlineKeyboardButton>> sickEndKeyboard = CreateCalendarKeyboard(DateTime.UtcNow.AddHours(3), "sick_end");

                        await botClient.SendTextMessageAsync(
                            chatId: e.CallbackQuery.From.Id,
                            text: "До какого дня поставить сыкливого (sick leave)?\nВыбери последний день больничного",
                            replyMarkup: new InlineKeyboardMarkup(sickEndKeyboard)
                            );
                        break;

                    case "sick_end":

                        await MessageDeletionOnCallback(e);
                        long sickEndTicks = long.Parse(e.CallbackQuery.Data.Substring(8));

                        sickLeaveEvents[e.CallbackQuery.From.Id].End = new DateTime(sickEndTicks);
                        sickLeaveEvents[e.CallbackQuery.From.Id].Name = users[e.CallbackQuery.From.Id];

                        SickLeaveEvent sickLeave = sickLeaveEvents[e.CallbackQuery.From.Id];


                        Event sickEvent = new Event
                        {
                            Summary = $"{sickLeave.Name}'s sick leave",
                            Start = new EventDateTime()
                            {
                                Date = string.Format("{0:yyyy-MM-dd}", sickLeave.Start)
                            },
                            End = new EventDateTime()
                            {
                                Date = string.Format("{0:yyyy-MM-dd}", sickLeave.End.AddDays(1))
                            },
                            Description = "Event created using telegram bot"
                        };

                        if (sickLeave.Start <= sickLeave.End)
                        {
                            Event recurringEvent = service.Events.Insert(sickEvent, calendarID).Execute();

                            InlineKeyboardButton[] delOrConfirmRow = new InlineKeyboardButton[]
                                {
                                    InlineKeyboardButton.WithCallbackData("✅ Оставить событие", "confirm_sick"),
                                    InlineKeyboardButton.WithCallbackData("❌ Удалить событие", $"delete{recurringEvent.Id}")
                                };

                            InlineKeyboardButton[][] kb_del_of_confirm = new InlineKeyboardButton[][]
                                {
                                    delOrConfirmRow
                                };

                            await botClient.SendTextMessageAsync(
                                    chatId: e.CallbackQuery.From.Id,
                                    parseMode: ParseMode.Markdown,
                                    text: $"Cобытие *[{sickEvent.Summary}]* было успешно создано \nс *{sickLeave.Start.ToShortDateString()}* по *{sickLeave.End.ToShortDateString()}*\n\nПоставил случайно или ошибся? Можно удалить!",
                                    replyMarkup: new InlineKeyboardMarkup(kb_del_of_confirm)
                                    );
                        }
                        else
                        {
                            SickLeaveEvent.Clear(sickLeaveEvents[e.CallbackQuery.From.Id]);
                            await botClient.SendTextMessageAsync(
                                    chatId: e.CallbackQuery.From.Id,
                                    text: $"Время окончания события указано раньше, время начала... попробуй ещё раз /start");
                        }

                        break;

                    case "start":
                        //0-16
                        await MessageDeletionOnCallback(e);

                        List<List<InlineKeyboardButton>> endHoursKeyboard = CreateEndHoursKeyboard(e.CallbackQuery.Message.Chat.Id);

                        await botClient.SendTextMessageAsync(
                            chatId: e.CallbackQuery.From.Id,
                            text: $"И до скольки?",
                            replyMarkup: new InlineKeyboardMarkup(endHoursKeyboard)
                        );
                        leaveRequestEvents[e.CallbackQuery.From.Id].Start = int.Parse(e.CallbackQuery.Data.Replace("start", string.Empty));
                        break;

                    case "end":
                        //1-18
                        await MessageDeletionOnCallback(e);

                        leaveRequestEvents[e.CallbackQuery.From.Id].End = int.Parse(e.CallbackQuery.Data.Replace("end", string.Empty));

                        if (users[e.CallbackQuery.From.Id] != null)
                        {
                            leaveRequestEvents[e.CallbackQuery.From.Id].Name = users[e.CallbackQuery.From.Id];
                        }
                        else
                        {
                            LeaveRequestEvent.Clear(leaveRequestEvents[e.CallbackQuery.From.Id]);
                            break;
                        }

                        string author;
                        if (e.CallbackQuery.From.Username != null)
                        {
                            author = e.CallbackQuery.From.Username;
                        }
                        else
                        {
                            author = $"[id]{e.CallbackQuery.From.Id}";
                        }

                        DateTime nowMSC = DateTime.UtcNow.AddHours(3);

                        int start_hour = 0;
                        int start_min = 0;

                        if (starts[e.CallbackQuery.From.Id] != null)
                        {
                            var parts = starts[e.CallbackQuery.From.Id].Split('.');
                            start_hour = int.Parse(parts[0]);
                            start_min = int.Parse(parts[1]);
                        }
                        DateTime workdayBeginningUTC = new DateTime(nowMSC.Year, nowMSC.Month, nowMSC.Day, start_hour - 3, start_min, 0, DateTimeKind.Utc);

                        if (leaveRequestEvents[e.CallbackQuery.From.Id].Date == "tomorrow") { workdayBeginningUTC = workdayBeginningUTC.AddDays(1); }

                        DateTime start = workdayBeginningUTC.AddMinutes(leaveRequestEvents[e.CallbackQuery.From.Id].Start * 30);
                        DateTime end = workdayBeginningUTC.AddMinutes(leaveRequestEvents[e.CallbackQuery.From.Id].End * 30);

                        Event lrEvent = new Event
                        {
                            Summary = $"{leaveRequestEvents[e.CallbackQuery.From.Id].Name}'s leave request",
                            //Location = "Somewhere",
                            Start = new EventDateTime()
                            {
                                DateTime = start,
                                TimeZone = "Europe/Moscow"
                            },
                            End = new EventDateTime()
                            {
                                DateTime = end,
                                TimeZone = "Europe/Moscow"
                            },
                            Description = "Event created using telegram bot"
                        };

                        if (lrEvent.Start.DateTime < lrEvent.End.DateTime)
                        {
                            if (leaveRequestEvents[e.CallbackQuery.From.Id].Name != "")
                            {
                                Event recurringEvent = service.Events.Insert(lrEvent, calendarID).Execute();

                                string from = string.Format("{0:HH:mm}", start.AddHours(3));
                                string to = string.Format("{0:HH:mm}", end.AddHours(3));

                                InlineKeyboardButton[] delOrConfirmRow = new InlineKeyboardButton[]
                                    {
                                        InlineKeyboardButton.WithCallbackData("✅ Оставить событие", "confirm_lr"),
                                        InlineKeyboardButton.WithCallbackData("❌ Удалить событие", $"delete{recurringEvent.Id}")
                                    };

                                InlineKeyboardButton[][] kb_del_of_confirm = new InlineKeyboardButton[][]
                                    {
                                        delOrConfirmRow
                                    };

                                string day = (DateTime.UtcNow.AddHours(3).Date == lrEvent.Start.DateTime.Value.Date) ? "Сегодня" : "Завтра";
                                await botClient.SendTextMessageAsync(
                                        chatId: e.CallbackQuery.From.Id,
                                        parseMode: ParseMode.Markdown,
                                        text: $"Cобытие *[{lrEvent.Summary}]* было успешно создано \n{day}, с *{from}* до *{to}*\n\nПоставил случайно или ошибся? Можно удалить!",
                                        replyMarkup: new InlineKeyboardMarkup(kb_del_of_confirm)
                                        );
                            }
                            else
                            {
                                LeaveRequestEvent.Clear(leaveRequestEvents[e.CallbackQuery.From.Id]);
                                await botClient.SendTextMessageAsync(
                                        chatId: e.CallbackQuery.From.Id,
                                        text: $"Что-то пошло не так... попробуй ещё раз /start");
                            }
                        }
                        else
                        {
                            LeaveRequestEvent.Clear(leaveRequestEvents[e.CallbackQuery.From.Id]);
                            await botClient.SendTextMessageAsync(
                                    chatId: e.CallbackQuery.From.Id,
                                    text: $"Время окончания события указано раньше, время начала... попробуй ещё раз /start");
                        }

                        break;

                    case "пн":
                    case "вт":
                    case "ср":
                    case "чт":
                    case "пт":
                    case "сб":
                    case "вс":
                    case "calendar_empty":
                    case "calendar_dateRow":
                        await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        break;

                    case "cancel":
                    case "ok_show":
                    case "ok_deletion":
                    case "ok_host":
                    case "cancel_deletion":
                    case "confirm_lr":  
                    case "confirm_sick":
                    case "cancel_start_hours":
                    case "cancel_end_hours":
                    case "cancel_days":
                    case "cancel_sick":
                    case "cancel_calendar":

                        await MessageDeletionOnCallback(e);

                        SickLeaveEvent.Clear(sickLeaveEvents[e.CallbackQuery.From.Id]);
                        LeaveRequestEvent.Clear(leaveRequestEvents[e.CallbackQuery.From.Id]);
                        break;

                    default:

                        await MessageDeletionOnCallback(e);

                        if (e.CallbackQuery.Data.Substring(0, 6) == "delete")
                        {
                            string eventId = e.CallbackQuery.Data.Substring(6);
                            try
                            {
                                service.Events.Delete(calendarID, eventId).Execute();
                                await botClient.SendTextMessageAsync(
                                chatId: e.CallbackQuery.From.Id,
                                text: $"Событие удалено!");
                            }
                            catch (Google.GoogleApiException ex)
                            {
                                if (ex.Error.Code == 410)
                                {
                                    await botClient.SendTextMessageAsync(
                                    chatId: e.CallbackQuery.From.Id,
                                    text: "Событие уже было удалено!");

                                    Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Event with id {eventId} has already been deleted");
                                }
                                else
                                {
                                    Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Exeption: {ex.Error.Message}, {ex.Error.Code}");
                                }
                            }
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(
                                chatId: e.CallbackQuery.From.Id,
                                text: "Я не знаю такой команды, смотри /help"
                            );
                        }

                        LeaveRequestEvent.Clear(leaveRequestEvents[e.CallbackQuery.From.Id]);
                        SickLeaveEvent.Clear(sickLeaveEvents[e.CallbackQuery.From.Id]);
                        break;
                }
            }
        }

        private static async System.Threading.Tasks.Task MessageDeletionOnCallback(CallbackQueryEventArgs e)
        {
            if ((DateTime.UtcNow - e.CallbackQuery.Message.Date).TotalHours < 48)
            {
                try
                {
                    await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    await botClient.DeleteMessageAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId);
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                {
                    if (ex.ErrorCode == 400)
                    {
                        Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Message with id {e.CallbackQuery.Message.MessageId} has already been deleted");
                    }
                    else
                    {
                        Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Exception :( StackTrace: {ex.StackTrace}");
                    }
                }
            }
            else
            {
                await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id, "удоли через контекстное меню");
                Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Old message deletion attempt {e.CallbackQuery.Message.Date} in chat with {users[e.CallbackQuery.From.Id]}");
                LeaveRequestEvent.Clear(leaveRequestEvents[e.CallbackQuery.From.Id]);
            }
        }

        private static List<List<InlineKeyboardButton>> CreateEndHoursKeyboard(long chatId)
        {
            List<InlineKeyboardButton> endButtonRow5 = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, 17), "end17" ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, 18), "end18" )
                };

            List<InlineKeyboardButton> cancelEndHoursButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_end_hours")
                };

            return new List<List<InlineKeyboardButton>>
                {
                                CreateEndButtonRow(chatId, 0),
                                CreateEndButtonRow(chatId, 1),
                                CreateEndButtonRow(chatId, 2),
                                CreateEndButtonRow(chatId, 3),
                                endButtonRow5,
                                cancelEndHoursButtonRow
                };
        }
        private static List<InlineKeyboardButton> CreateEndButtonRow(long chatId, int rowId)
        {
            return new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 1), "end" + (rowId * 4 + 1).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 2), "end" + (rowId * 4 + 2).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 3), "end" + (rowId * 4 + 3).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 4), "end" + (rowId * 4 + 4).ToString() )
                };
        }

        private static List<List<InlineKeyboardButton>> CreateStartHoursKeyboard(long chatId)
        {
            List<InlineKeyboardButton> startButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, 0), "start0")
                };

            List<InlineKeyboardButton> cancelStartHoursButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_start_hours")
                };

            return new List<List<InlineKeyboardButton>>
                            {
                                startButtonRow,
                                CreateStartButtonRow(chatId, 0),
                                CreateStartButtonRow(chatId, 1),
                                CreateStartButtonRow(chatId, 2),
                                CreateStartButtonRow(chatId, 3),
                                cancelStartHoursButtonRow
                            };
        }
        private static List<InlineKeyboardButton> CreateStartButtonRow(long chatId, int rowId)
        {
            return new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 1), "start" + (rowId * 4 + 1).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 2), "start" + (rowId * 4 + 2).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 3), "start" + (rowId * 4 + 3).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 4), "start" + (rowId * 4 + 4).ToString() )
                };
        }
        private static string TimeValue(long chatId, int delta)
        {
            int start_hour = 0;
            int start_min = 0;

            if (starts[chatId] != null)
            {
                var parts = starts[chatId].Split('.');
                start_hour = int.Parse(parts[0]);
                start_min = int.Parse(parts[1]);
            }

			var t = new DateTime(1900, 1, 1, start_hour, start_min, 0).AddMinutes(30 * delta);

            string mins_with_zero;
            if (t.Minute == 0)
            {
                mins_with_zero = "00";
            }
            else
            {
                mins_with_zero = t.Minute.ToString();
            }

            string[] time = { t.Hour.ToString(), mins_with_zero };

            return string.Join(":", time);
        }
    }
}