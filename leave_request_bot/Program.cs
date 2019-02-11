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
		static CalendarEvent calendarEvent = new CalendarEvent();
		static readonly string[] Scopes = { CalendarService.Scope.CalendarEvents };
		static string ApplicationName = "Google Calendar API - DKOR TLGRM BOT";
		static CalendarService service;
		static readonly string calendarID = ConfigurationManager.AppSettings["calendarID"];
		static readonly string token = ConfigurationManager.AppSettings["token"];
        static readonly string botLaunchTimeUTC = string.Format("{0:[HH:mm:ss] dd.MM.yyyy}", DateTime.UtcNow);
        static readonly Dictionary<int, string> users = File.ReadLines(Helper.FullPathToFile(@"users.csv")).Select(line => line.Split(';')).ToDictionary(line => int.Parse(line[0]), line => line[1]);
        static readonly Dictionary<long, string> starts = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => line[1]);

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
                CalendarEvent.Clear(calendarEvent);

                switch (e.Message.Text)
				{
					case "/help":
					case "/?":
                        botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: "Бот создает событие в календаре *\"Leave Requests\"* [[SED]]\nПришли мне /start",
                            parseMode: ParseMode.Markdown
                        );
                        break;
                    case "/host":
                        string ip = Dns.GetHostAddresses(Environment.MachineName).Last().ToString();
                        botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: "Hostame: *" + Environment.MachineName + " *\nIP: " + ip + "\nStarted [UTC]: " + botLaunchTimeUTC + "\nWITHOUT PROXY",
                            parseMode: ParseMode.Markdown
                        );
                        break;



                    case "/del":
                        var rq = service.Events.List(calendarID);
                        rq.TimeMin = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                        IList<Event> allEvents = rq.Execute().Items;

                        IList<Event> userEvents = allEvents.Where(ev => ev.Creator.Email == "dmitri.korneev@cbsinteractive.com" 
                            && ev.Summary == $"{users[e.Message.From.Id]}'s leave request").OrderBy(ev => ev.Start.DateTime).ToList();

                        InlineKeyboardButton[][] kb_del = new InlineKeyboardButton[userEvents.Count+1][];

                        var txt = "Нет событий для удаления...";

                        if (userEvents.Count > 0)
                        {
                            int i = 0;
                            txt = "Выбери событие которое нужно удалить:";
                            foreach (Event ev in userEvents)
                            {
                                string day;
                                if (string.Format("{0:dd/MM/yy}", ev.Start.DateTime) == string.Format("{0:dd/MM/yy}", DateTime.UtcNow.AddHours(3))) { day = "сегодня"; }
                                else if (string.Format("{0:dd/MM/yy}", ev.Start.DateTime) == string.Format("{0:dd/MM/yy}", DateTime.UtcNow.AddHours(3).AddDays(1))) { day = "завтра"; }
                                else { day = string.Format("{0:dd/MM}", ev.Start.DateTime); }
                                kb_del[i] = new InlineKeyboardButton[]
                                    {
                                        InlineKeyboardButton.WithCallbackData($"{day} | {string.Format("{0:HH:mm}", ev.Start.DateTime)} - {string.Format("{0:HH:mm}", ev.End.DateTime)}", $"delete{ev.Id}")
                                    };
                                i++;
                            }
                        }

                        kb_del[userEvents.Count] = new InlineKeyboardButton[]
                        {
                            InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                        };

                        botClient.SendTextMessageAsync(
                            chatId: e.Message.Chat,
                            text: txt,
                            replyMarkup: new InlineKeyboardMarkup(kb_del),
                            parseMode: ParseMode.Markdown
                        );
                        break;

                    case "/start":
						InlineKeyboardButton[] cancelButtonRow = new InlineKeyboardButton[]
							{
								InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
							};

						InlineKeyboardButton[] daysButtonRow = new InlineKeyboardButton[]
							{
								InlineKeyboardButton.WithCallbackData("сегодня", "today"),
								InlineKeyboardButton.WithCallbackData("завтра", "tomorrow")
							};

						InlineKeyboardButton[][] kb_days = new InlineKeyboardButton[][]
							{
								daysButtonRow,
								cancelButtonRow
							};

						botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Когда поставить leave request?",
							replyMarkup: new InlineKeyboardMarkup(kb_days)
							);
						break;

					default:
						botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Я не знаю такой команды, смотри /help"
						);
						break;
				}
			}
			if (!users.TryGetValue(e.Message.From.Id, out string value_1))
			{
				Console.WriteLine($"Received a text message from unauthorized user with id {e.Message.From.ToString()}.");
				botClient.SendTextMessageAsync(
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
                botClient.DeleteMessageAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId);

                switch (Regex.Replace(e.CallbackQuery.Data, @"[\d-]", string.Empty))
				{
					case "today":
					case "tomorrow":

                        InlineKeyboardButton[][] kb_start_hours = CreateStartHoursKeyboard(e.CallbackQuery.Message.Chat.Id);

						botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
						botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: $"Со скольки?",
							replyMarkup: new InlineKeyboardMarkup(kb_start_hours)
						);

						calendarEvent.Date = e.CallbackQuery.Data;
						break;

					case "start":
						//0-16
						InlineKeyboardButton[][] kb_end_hours = CreateEndHoursKeyboard(e.CallbackQuery.Message.Chat.Id);

                        botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
						botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: $"И до скольки?",
							replyMarkup: new InlineKeyboardMarkup(kb_end_hours)
						);
						calendarEvent.Start = int.Parse(e.CallbackQuery.Data.Replace("start", string.Empty));
						break;

					case "end":
						//1-18
						botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
						calendarEvent.End = int.Parse(e.CallbackQuery.Data.Replace("end", string.Empty));

						if (users[e.CallbackQuery.From.Id] != null)
						{
							calendarEvent.Name = users[e.CallbackQuery.From.Id];

                        }
						else
						{
							CalendarEvent.Clear(calendarEvent);
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

						if (calendarEvent.Date == "tomorrow") { workdayBeginningUTC = workdayBeginningUTC.AddDays(1); }

						DateTime start = workdayBeginningUTC.AddMinutes(calendarEvent.Start * 30);
						DateTime end = workdayBeginningUTC.AddMinutes(calendarEvent.End * 30);

						Event lrEvent = new Event
						{
							Summary = $"{calendarEvent.Name}'s leave request",
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
							if (calendarEvent.Name != "")
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
								botClient.SendTextMessageAsync(
										chatId: e.CallbackQuery.From.Id,
										parseMode: ParseMode.Markdown,
										text: $"Cобытие *[{lrEvent.Summary}]* было успешно создано \n{day}, с *{from}* до *{to}*\n\nПоставил случайно или ошибся? Можно удалить!",
										replyMarkup: new InlineKeyboardMarkup(kb_del_of_confirm)
										);
							}
							else
							{
								CalendarEvent.Clear(calendarEvent);
								botClient.SendTextMessageAsync(
										chatId: e.CallbackQuery.From.Id,
										text: $"Что-то пошло не так... попробуй ещё раз /start");
							}
						}
						else
						{
							CalendarEvent.Clear(calendarEvent);
							botClient.SendTextMessageAsync(
									chatId: e.CallbackQuery.From.Id,
									text: $"Время окончания события указано раньше, время начала... попробуй ещё раз /start");
						}

						break;

					case "cancel":
						botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
						CalendarEvent.Clear(calendarEvent);
						break;

					default:
						botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);

						if (e.CallbackQuery.Data.Substring(0,6) == "delete")
						{
							string eventId = e.CallbackQuery.Data.Substring(6);
                            try
                            {
                                service.Events.Delete(calendarID, eventId).Execute();
                                botClient.SendTextMessageAsync(
                                chatId: e.CallbackQuery.From.Id,
                                text: $"Событие удалено!");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Exeption: {ex.Message}");

                                botClient.SendTextMessageAsync(
                                chatId: e.CallbackQuery.From.Id,
                                text: $"Событие уже было удалено!");
                            }
						}
						else
						{
							botClient.SendTextMessageAsync(
								chatId: e.CallbackQuery.From.Id,
								text: "Я не знаю такой команды, смотри /help"
							);
						}

						CalendarEvent.Clear(calendarEvent);
						break;
				}
			}
		}

        private static InlineKeyboardButton[][] CreateEndHoursKeyboard(long chatId)
        {
            InlineKeyboardButton[] endButtonRow5 = new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("18:45", "end17"),
                    InlineKeyboardButton.WithCallbackData("19:15", "end18")
                };

            InlineKeyboardButton[] cancelButtonRow = new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                };
            return new InlineKeyboardButton[][]
                {
                                CreateEndButtonRow(chatId, 0),
                                CreateEndButtonRow(chatId, 1),
                                CreateEndButtonRow(chatId, 2),
                                CreateEndButtonRow(chatId, 3),
                                endButtonRow5,
                                cancelButtonRow
                };
        }

        private static InlineKeyboardButton[] CreateEndButtonRow(long chatId, int rowId)
        {
            return new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 1), "end" + (rowId * 4 + 1).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 2), "end" + (rowId * 4 + 2).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 3), "end" + (rowId * 4 + 3).ToString() ),
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, rowId * 4 + 4), "end" + (rowId * 4 + 4).ToString() )
                };
        }

        private static InlineKeyboardButton[][] CreateStartHoursKeyboard(long chatId)
        {
            InlineKeyboardButton[] startButtonRow = new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(TimeValue(chatId, 0), "start0")
                };

            InlineKeyboardButton[] cancelButtonRow = new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                };

            return new InlineKeyboardButton[][]
                            {
                                startButtonRow,
                                CreateStartButtonRow(chatId, 0),
                                CreateStartButtonRow(chatId, 1),
                                CreateStartButtonRow(chatId, 2),
                                CreateStartButtonRow(chatId, 3),
                                cancelButtonRow
                            };
        }

        private static InlineKeyboardButton[] CreateStartButtonRow(long chatId, int rowId)
        {
            return new InlineKeyboardButton[]
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
            if (min_with_delta == 0) { mins_with_zero = "00"; }
            else { mins_with_zero = min_with_delta.ToString(); }

            string[] time = { hour_with_delta.ToString(), mins_with_zero };
            return string.Join(":", time);

        }
    }
}