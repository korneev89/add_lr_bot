using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
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
		static readonly Dictionary<int, string> users = File.ReadLines(Helper.FullPathToFile(@"users.csv")).Select(line => line.Split(';')).ToDictionary(line => int.Parse(line[0]), line => line[1]);

		static void Main()
		{
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
				ApplicationName = ApplicationName,
			});

			var me = botClient.GetMeAsync().Result;
			Console.WriteLine(
			  $"Hello, World! I am user {me.Id} and my name is {me.FirstName}."
			);

			botClient.OnMessage += Bot_OnMessage;
			botClient.OnCallbackQuery += Bot_OnCallback;
			botClient.StartReceiving();
			Thread.Sleep(int.MaxValue);
		}
		static async void Bot_OnMessage(object sender, MessageEventArgs e)
		{
			if ( e.Message.Text != null && users.TryGetValue(e.Message.From.Id, out string value))
			{
				Console.WriteLine($"Received a text message in chat {e.Message.Chat.Id}.");
				CalendarEvent.Clear(calendarEvent);

				switch (e.Message.Text)
				{
					case "/help":
					case "/?":
						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Бот создает событие в календаре *\"Leave Requests\"* [[SED]]\nПришли мне /start",
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

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Когда поставить leave request?",
							replyMarkup: new InlineKeyboardMarkup(kb_days)
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
				Console.WriteLine($"Received a text message from unauthorized user with id {e.Message.From.Id}.");
				await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Дружок, ты не авторизован! Попроси [Димана](tg://user?id=168694373), чтобы добавил тебя",
							parseMode: ParseMode.Markdown
						);
			}
		}

		static async void Bot_OnCallback(object sender, CallbackQueryEventArgs e)
		{
			InlineKeyboardButton[] startButtonRow1 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("10:45", "start1"),
					InlineKeyboardButton.WithCallbackData("11:15", "start2"),
					InlineKeyboardButton.WithCallbackData("11:45", "start3"),
					InlineKeyboardButton.WithCallbackData("12:15", "start4")
				};

			InlineKeyboardButton[] startButtonRow2 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("12:45", "start5"),
					InlineKeyboardButton.WithCallbackData("13:15", "start6"),
					InlineKeyboardButton.WithCallbackData("13:45", "start7"),
					InlineKeyboardButton.WithCallbackData("14:15", "start8")
				};

			InlineKeyboardButton[] startButtonRow3 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("14:45", "start9"),
					InlineKeyboardButton.WithCallbackData("15:15", "start10"),
					InlineKeyboardButton.WithCallbackData("15:45", "start11"),
					InlineKeyboardButton.WithCallbackData("16:15", "start12")
				};

			InlineKeyboardButton[] startButtonRow4 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("16:45", "start13"),
					InlineKeyboardButton.WithCallbackData("17:15", "start14"),
					InlineKeyboardButton.WithCallbackData("17:45", "start15"),
					InlineKeyboardButton.WithCallbackData("18:15", "start16")
				};

			InlineKeyboardButton[] endButtonRow1 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("10:45", "end1"),
					InlineKeyboardButton.WithCallbackData("11:15", "end2"),
					InlineKeyboardButton.WithCallbackData("11:45", "end3"),
					InlineKeyboardButton.WithCallbackData("12:15", "end4")
				};

			InlineKeyboardButton[] endButtonRow2 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("12:45", "end5"),
					InlineKeyboardButton.WithCallbackData("13:15", "end6"),
					InlineKeyboardButton.WithCallbackData("13:45", "end7"),
					InlineKeyboardButton.WithCallbackData("14:15", "end8")
				};

			InlineKeyboardButton[] endButtonRow3 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("14:45", "end9"),
					InlineKeyboardButton.WithCallbackData("15:15", "end10"),
					InlineKeyboardButton.WithCallbackData("15:45", "end11"),
					InlineKeyboardButton.WithCallbackData("16:15", "end12")
				};

			InlineKeyboardButton[] endButtonRow4 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("16:45", "end13"),
					InlineKeyboardButton.WithCallbackData("17:15", "end14"),
					InlineKeyboardButton.WithCallbackData("17:45", "end15"),
					InlineKeyboardButton.WithCallbackData("18:15", "end16")
				};

			InlineKeyboardButton[] endButtonRow5 = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("18:45", "end17"),
					InlineKeyboardButton.WithCallbackData("19:15", "end18")
				};

			InlineKeyboardButton[] cancelButtonRow = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
				};

			InlineKeyboardButton[] startButtonRow = new InlineKeyboardButton[]
				{
					InlineKeyboardButton.WithCallbackData("10:15", "start0")
				};

			if (e.CallbackQuery.Data != null)
			{
				Console.WriteLine($"Received a callback query {e.CallbackQuery.Data} in chat {e.CallbackQuery.Message.Chat.Id}.");

				switch (Regex.Replace(e.CallbackQuery.Data, @"[\d-]", string.Empty))
					//e.CallbackQuery.Data)
				{

					case "today":
					case "tomorrow":

						InlineKeyboardButton[][] kb_start_hours = new InlineKeyboardButton[][]
							{
								startButtonRow,
								startButtonRow1,
								startButtonRow2,
								startButtonRow3,
								startButtonRow4,
								cancelButtonRow
							};

						await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
						await botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: $"Со скольки?",
							replyMarkup: new InlineKeyboardMarkup(kb_start_hours)
						);

						calendarEvent.Date = e.CallbackQuery.Data;
						break;

					case "start":
						//0-16
						InlineKeyboardButton[][] kb_end_hours = new InlineKeyboardButton[][]
							{
								endButtonRow1,
								endButtonRow2,
								endButtonRow3,
								endButtonRow4,
								endButtonRow5,
								cancelButtonRow
							};

						await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
						await botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: $"И до скольки?",
							replyMarkup: new InlineKeyboardMarkup(kb_end_hours)
						);
						calendarEvent.Start = int.Parse(e.CallbackQuery.Data.Replace("start", string.Empty));
						break;

					case "end":
						//1-18
						await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
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

						DateTime nowUTC = DateTime.UtcNow;
						DateTime workdayBeginningUTC = new DateTime(nowUTC.Year, nowUTC.Month, nowUTC.Day, 7, 15, 0, DateTimeKind.Utc);

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
								TimeZone = TimeZoneInfo.Utc.StandardName
							},
							End = new EventDateTime()
							{
								DateTime = end,
								TimeZone = TimeZoneInfo.Utc.StandardName
							},
							Description = "Event created using telegram bot"
						};

						if (lrEvent.Start.DateTime < lrEvent.End.DateTime)
						{
							if (calendarEvent.Name != "")
							{
								Event recurringEvent = service.Events.Insert(lrEvent, calendarID).Execute();

								//string from = string.Format("{0:[HH:mm] dd.MM.yy}", lrEvent.Start.DateTime);
								//string to = string.Format("{0:[HH:mm] dd.MM.yy}", lrEvent.End.DateTime);
								//show Moscow Time
								string from = string.Format("{0:HH:mm}", lrEvent.Start.DateTime.Value.ToUniversalTime().AddHours(3));
								string to = string.Format("{0:HH:mm}", lrEvent.End.DateTime.Value.ToUniversalTime().AddHours(3));

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
								CalendarEvent.Clear(calendarEvent);
								await botClient.SendTextMessageAsync(
										chatId: e.CallbackQuery.From.Id,
										text: $"Что-то пошло не так... попробуй ещё раз /start");
							}
						}
						else
						{
							CalendarEvent.Clear(calendarEvent);
							await botClient.SendTextMessageAsync(
									chatId: e.CallbackQuery.From.Id,
									text: $"Время окончания события указано раньше, время начала... попробуй ещё раз /start");
						}

						break;

					case "cancel":
						await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
						CalendarEvent.Clear(calendarEvent);
						break;

					default:
						await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);

						if (e.CallbackQuery.Data.Substring(0,6) == "delete")
						{
							string eventId = e.CallbackQuery.Data.Substring(6);
							service.Events.Delete(calendarID, eventId).Execute();

							await botClient.SendTextMessageAsync(
								chatId: e.CallbackQuery.From.Id,
								text: $"Событие было удалено!");
						}
						else
						{
							await botClient.SendTextMessageAsync(
								chatId: e.CallbackQuery.From.Id,
								text: "Я не знаю такой команды, смотри /help"
							);
						}

						CalendarEvent.Clear(calendarEvent);
						break;
				}

				await botClient.DeleteMessageAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId);
			}
		}

		//private static string CreateTrueURL(CalendarEvent calendarEvent)
		//{
		//	DateTime today = DateTime.UtcNow.AddHours(3);
		//	var start = new DateTime(today.Year, today.Month, today.Day, 10, 15, 0);
		//	start.AddMinutes(calendarEvent.Start * 30);
		//	if (calendarEvent.Date == "tomorrow") { start = start.AddDays(1); }

		//	var end = start.AddMinutes(calendarEvent.End * 30);

		//	string s = string.Format("{0:yyyyMMddTHHmmss}", start);
		//	string e = string.Format("{0:yyyyMMddTHHmmss}", end);

		//	return $"https://calendar.google.com/calendar/r/eventedit?text={calendarEvent.Name}'s leave request&dates={s}/{e}&details=Event created using telegram bot&src=cbsinteractive.com_rpv5nbaapi6193eihuqg0ucvg0%40group.calendar.google.com";
		//}

		// USING CHROME AND SELENIUM

		//ChromeOptions options = new ChromeOptions();

		////options.AddArgument("headless");
		////options.AddArgument("--window-size=1280,960");

		//options.AddArguments("user-data-dir=/chrome-profile");
		//string drvPath = ConfigurationManager.AppSettings["drvPath"];
		//ChromeDriver driver = new ChromeDriver(drvPath, options)
		//{
		//	Url = url
		//};

		//try
		//{
		//	//Thread.Sleep(100000);
		//	driver.FindElement(By.CssSelector("#xSaveBu")).Click();
		//	await botClient.SendTextMessageAsync(
		//		chatId: e.CallbackQuery.From.Id,
		//		text: $"{calendarEvent.Name} {calendarEvent.Date} ebalo v podushku (10:15 -> {10 + int.Parse(calendarEvent.Hours)}:15)\n\nRequest has been add to calendar");
		//}
		//catch (Exception ex)
		//{
		//	await botClient.SendTextMessageAsync(
		//		chatId: e.CallbackQuery.From.Id,
		//		text: $"Диман не умеет кодить - лови эксепцию!\n\n{ex.Message}");
		//}
		//finally
		//{
		//	driver.Quit();
		//}
	}
}









