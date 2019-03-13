using System;
using System.Collections.Generic;
using System.Configuration;
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
        static readonly Dictionary<long, CalendarEvent> calendarEvents = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => new CalendarEvent());

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
			  $"Hello, World! I am user {me.Id} and my name is {me.FirstName}."
			);

            string str = Dns.GetHostAddresses(Environment.MachineName).Last().ToString();
            botClient.SendTextMessageAsync(users.First().Key, "Чувак, я жив!\nHostame: *" + Environment.MachineName + "*\nIP: " + str + "\n\nWITHOUT PROXY ", ParseMode.Markdown);

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
                CalendarEvent.Clear(calendarEvents[e.Message.From.Id]);

                List<InlineKeyboardButton> cancelButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                };

                List<InlineKeyboardButton> okButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("✅ ОK", "cancel")
                };

                switch (e.Message.Text)
				{
					case "/help":
					case "/?":
                       await botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: "Утром лицо застряло в текстурах подушки? Бот добавит/удалит событие в календаре [[SED]] *\"Leave Requests\"*" +
                            "\n\n/start \"Диме плохо\" или добавить событие" +
                            "\n/del если выбрался из текстур и нужно удалить событие" +
                            "\n/show те кто не смог или не сможет завтра",
                            parseMode: ParseMode.Markdown
                        );
                        break;
                    case "/host":
                       string ip = Dns.GetHostAddresses(Environment.MachineName).Last().ToString();
                       await botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: "Hostame: *" + Environment.MachineName + " *\nIP: " + ip + "\nStarted [UTC]: " + botLaunchTimeUTC + "\nWITHOUT PROXY",
                            parseMode: ParseMode.Markdown
                        );
                        break;

                    case "/del":
                        var rq = service.Events.List(calendarID);
                        rq.TimeMin = DateTime.UtcNow.Date;
                        IList<Event> allEvents = rq.Execute().Items;

                        var txt = "Нет событий для удаления...";

                        List<List<InlineKeyboardButton>> deletionKeyboard = new List<List<InlineKeyboardButton>>();

                        if (allEvents.Count > 0)
                        {
                            IList<Event> userEvents = allEvents.Where(ev => ev.Creator?.Email == "dmitri.korneev@cbsinteractive.com"
                              && ev.Summary == $"{users[e.Message.From.Id]}'s leave request")
                              .OrderBy(ev => ev.Start.DateTime)
                              .ThenBy(ev => ev.End.DateTime)
                              .ToList();

                            if (userEvents.Count > 0)
                            {
                                txt = "Выбери событие которое нужно удалить:";

                                foreach (Event ev in userEvents)
                                {
                                    string day;
                                    int delta = 3 - (DateTime.Now.Hour - DateTime.UtcNow.Hour); // 3 hour - for MSC time shift

                                    if ( ev.Start.DateTime.Value.Date == DateTime.UtcNow.AddHours(3).Date )
                                    {
                                        day = "сегодня";
                                    }
                                    else if ( ev.Start.DateTime.Value.Date == DateTime.UtcNow.AddHours(3).AddDays(1).Date )
                                    {
                                        day = "завтра";
                                    }
                                    else
                                    {
                                        day = string.Format("{0:dd/MM}", ev.Start.DateTime);
                                    }

                                    List<InlineKeyboardButton> buttonRow = new List<InlineKeyboardButton>
                                    {
                                        InlineKeyboardButton.WithCallbackData($"{day} | {string.Format("{0:HH:mm}", ev.Start.DateTime.Value.AddHours(delta))} - {string.Format("{0:HH:mm}", ev.End.DateTime.Value.AddHours(delta))}", $"delete{ev.Id}")
                                    };

                                    deletionKeyboard.Add(buttonRow);
                                }
                                deletionKeyboard.Add(cancelButtonRow);
                            }
                        }

                        else
                        {
                            deletionKeyboard.Add(okButtonRow);
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
                            ThenBy(ev => ev.Summary).
                            ToList();

                        var msg = "На сегодня и завтра нет созданных событий 🤷‍♂️";

                        if (evs.Count > 0)
                        {

                            msg = "Кто рано встает - их тут нет:\n";
                            foreach (Event ev in evs)
                            {
                                string day;
                                int delta = 3 - (DateTime.Now.Hour - DateTime.UtcNow.Hour); // 3 hour - for MSC time shift
                                if (string.Format("{0:dd/MM/yy}", ev.Start.DateTime) == string.Format("{0:dd/MM/yy}", DateTime.UtcNow.AddHours(3))) { day = "сегодня"; }
                                else if (string.Format("{0:dd/MM/yy}", ev.Start.DateTime) == string.Format("{0:dd/MM/yy}", DateTime.UtcNow.AddHours(3).AddDays(1))) { day = "завтра"; }
                                else { day = string.Format("{0:dd/MM}", ev.Start.DateTime); }

                                msg += $"\n{ev.Summary.Replace("'s leave request", string.Empty)} | {day} | {string.Format("{0:HH:mm}", ev.Start.DateTime.Value.AddHours(delta))} - {string.Format("{0:HH:mm}", ev.End.DateTime.Value.AddHours(delta))}";
                            }
                        }

                        List<List<InlineKeyboardButton>> showKeyboard = new List<List<InlineKeyboardButton>>
                        {
                            okButtonRow
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
								cancelButtonRow
							};

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Когда поставить leave request?",
							replyMarkup: new InlineKeyboardMarkup(daysKeyboard)
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
				Console.WriteLine($"Received a text message from unauthorized user with id {e.Message.From.ToString()}.");
                await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: $"Дружок, ты не авторизован! Попроси [Димана](tg://user?id=168694373), чтобы добавил тебя. Твой id {e.Message.From.Id}",
							parseMode: ParseMode.Markdown
						);
			}
		}

		static async void Bot_OnCallback(object sender, CallbackQueryEventArgs e)
		{
			if (e.CallbackQuery.Data != null)
			{
				Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Received a callback query \"{e.CallbackQuery.Data}\" in chat with {users[e.CallbackQuery.From.Id]}.");
                await botClient.DeleteMessageAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId);

                switch (Regex.Replace(e.CallbackQuery.Data, @"[\d-]", string.Empty))
				{
					case "today":
					case "tomorrow":

                        List<List<InlineKeyboardButton>> startHoursKeyboard = CreateStartHoursKeyboard(e.CallbackQuery.Message.Chat.Id);

						await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        await botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: $"Со скольки?",
							replyMarkup: new InlineKeyboardMarkup(startHoursKeyboard)
						);
                        calendarEvents[e.CallbackQuery.From.Id].Date = e.CallbackQuery.Data;
						break;

					case "start":
						//0-16
						List<List<InlineKeyboardButton>> endHoursKeyboard = CreateEndHoursKeyboard(e.CallbackQuery.Message.Chat.Id);

                        await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        await botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: $"И до скольки?",
							replyMarkup: new InlineKeyboardMarkup(endHoursKeyboard)
						);
                        calendarEvents[e.CallbackQuery.From.Id].Start = int.Parse(e.CallbackQuery.Data.Replace("start", string.Empty));
						break;

					case "end":
                        //1-18
                        await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        calendarEvents[e.CallbackQuery.From.Id].End = int.Parse(e.CallbackQuery.Data.Replace("end", string.Empty));

						if (users[e.CallbackQuery.From.Id] != null)
						{
                            calendarEvents[e.CallbackQuery.From.Id].Name = users[e.CallbackQuery.From.Id];
                        }
						else
						{
                            CalendarEvent.Clear(calendarEvents[e.CallbackQuery.From.Id]);
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

						if (calendarEvents[e.CallbackQuery.From.Id].Date == "tomorrow") { workdayBeginningUTC = workdayBeginningUTC.AddDays(1); }

						DateTime start = workdayBeginningUTC.AddMinutes(calendarEvents[e.CallbackQuery.From.Id].Start * 30);
						DateTime end = workdayBeginningUTC.AddMinutes(calendarEvents[e.CallbackQuery.From.Id].End * 30);

						Event lrEvent = new Event
						{
							Summary = $"{calendarEvents[e.CallbackQuery.From.Id].Name}'s leave request",
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
							if (calendarEvents[e.CallbackQuery.From.Id].Name != "")
							{
								Event recurringEvent = service.Events.Insert(lrEvent, calendarID).Execute();

								string from = string.Format("{0:HH:mm}", start.AddHours(3));
								string to = string.Format("{0:HH:mm}", end.AddHours(3));

								InlineKeyboardButton[] delOrConfirmRow = new InlineKeyboardButton[]
									{
										InlineKeyboardButton.WithCallbackData("✅ Оставить", "cancel"),
										InlineKeyboardButton.WithCallbackData("❌ Удалить", $"delete{recurringEvent.Id}")
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
                                CalendarEvent.Clear(calendarEvents[e.CallbackQuery.From.Id]);
                                await botClient.SendTextMessageAsync(
										chatId: e.CallbackQuery.From.Id,
										text: $"Что-то пошло не так... попробуй ещё раз /start");
							}
						}
						else
						{
                            CalendarEvent.Clear(calendarEvents[e.CallbackQuery.From.Id]);
                            await botClient.SendTextMessageAsync(
									chatId: e.CallbackQuery.From.Id,
									text: $"Время окончания события указано раньше, время начала... попробуй ещё раз /start");
						}

						break;

					case "cancel":
                        await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        CalendarEvent.Clear(calendarEvents[e.CallbackQuery.From.Id]);
                        break;

					default:
                        await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);

						if (e.CallbackQuery.Data.Substring(0,6) == "delete")
						{
							string eventId = e.CallbackQuery.Data.Substring(6);
                            try
                            {
                                service.Events.Delete(calendarID, eventId).Execute();
                                await botClient.SendTextMessageAsync(
                                chatId: e.CallbackQuery.From.Id,
                                text: $"Событие удалено!");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Exeption: {ex.Message}");

                                await botClient.SendTextMessageAsync(
                                chatId: e.CallbackQuery.From.Id,
                                text: $"Событие уже было удалено!");
                            }
						}
						else
						{
                            await botClient.SendTextMessageAsync(
								chatId: e.CallbackQuery.From.Id,
								text: "Я не знаю такой команды, смотри /help"
							);
						}

                        CalendarEvent.Clear(calendarEvents[e.CallbackQuery.From.Id]);
                        break;
				}
			}
		}

        private static List<List<InlineKeyboardButton>> CreateEndHoursKeyboard(long chatId)
        {
            List<InlineKeyboardButton> endButtonRow5 = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, 17), "end17" ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, 18), "end18" )
                };

            List<InlineKeyboardButton> cancelButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                };

            return new List<List<InlineKeyboardButton>>
                {
                                CreateEndButtonRow(chatId, 0),
                                CreateEndButtonRow(chatId, 1),
                                CreateEndButtonRow(chatId, 2),
                                CreateEndButtonRow(chatId, 3),
                                endButtonRow5,
                                cancelButtonRow
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

            List<InlineKeyboardButton> cancelButtonRow = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                };

            return new List<List<InlineKeyboardButton>>
                            {
                                startButtonRow,
                                CreateStartButtonRow(chatId, 0),
                                CreateStartButtonRow(chatId, 1),
                                CreateStartButtonRow(chatId, 2),
                                CreateStartButtonRow(chatId, 3),
                                cancelButtonRow
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

            int hour_with_delta = start_hour + (30 * delta) / 60;
            int min_with_delta = (start_min + 30 * delta) % 60;
            string mins_with_zero;
            if (min_with_delta == 0)
            {
                mins_with_zero = "00";
            }
            else
            {
                mins_with_zero = min_with_delta.ToString();
            }

            string[] time = { hour_with_delta.ToString(), mins_with_zero };

            return string.Join(":", time);
        }
    }
}