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
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace LeaveRequestsBot
{
	public class Program
	{
		static ITelegramBotClient botClient;
		static readonly string[] Scopes = { CalendarService.Scope.CalendarEvents };
		static string ApplicationName = "Google Calendar API - DKOR TLGRM BOT";
		static CalendarService _service;
		static readonly string calendarId = ConfigurationManager.AppSettings["calendarID"];
		static readonly string token = ConfigurationManager.AppSettings["token"];
		static readonly string botLaunchTimeUTC = string.Format("{0:[HH:mm:ss] dd.MM.yyyy}", DateTime.UtcNow);
		static readonly Dictionary<int, string> users = File.ReadLines(Helper.FullPathToFile(@"users.csv")).Select(line => line.Split(';')).ToDictionary(line => int.Parse(line[0]), line => line[1]);
		static readonly Dictionary<long, string> starts = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => line[1]);
		static readonly Dictionary<long, LeaveRequestEvent> leaveRequestEvents = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => new LeaveRequestEvent());
		static readonly Dictionary<long, SickLeaveEvent> sickLeaveEvents = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => new SickLeaveEvent());
		static readonly Dictionary<long, SickLeaveEvent> dayoffEvents = File.ReadLines(Helper.FullPathToFile(@"starts.csv")).Select(line => line.Split(';')).ToDictionary(line => long.Parse(line[0]), line => new SickLeaveEvent());
		// check that NumberOfTimeButtons * TimeButtonsDiff == 60
		private static readonly int NumberOfTimeButtons = 6;
		private static readonly int TimeButtonsDiff = 10;
		private static KeyboardBuilder _kbBuilder;

		public static void Main()
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

			_kbBuilder = new KeyboardBuilder(
				NumberOfTimeButtons,
				TimeButtonsDiff,
				starts);

			_service = new CalendarService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName
			});

			var me = botClient.GetMeAsync().Result;
			Console.WriteLine(
				$"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Hello, World! I am user {me.Id} and my name is {me.FirstName}."
			);

			var str = Dns.GetHostAddresses(Environment.MachineName).Last().ToString();
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
						var ip = Dns.GetHostAddresses(Environment.MachineName).Last().ToString();

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Hostame: *" + Environment.MachineName + " *\nIP: " + ip + "\nStarted [UTC]: " + botLaunchTimeUTC,
							parseMode: ParseMode.Markdown,
							replyMarkup: new InlineKeyboardMarkup(_kbBuilder.HostKeyboard())
						);
						break;

					case "/del":

						var deletionKeyboard = _kbBuilder.DeletionKeyboard(e, calendarId, _service, users);
						
						var text = "Нет событий для удаления...";
						if (deletionKeyboard.Count > 1)
						{
							text = "Выбери событие которое нужно удалить:";
						}

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: text,
							replyMarkup: new InlineKeyboardMarkup(deletionKeyboard),
							parseMode: ParseMode.Markdown
						);
						break;

					case "/show":
						var req = _service.Events.List(calendarId);
						req.TimeMin = DateTime.UtcNow.Date;
						var events = req.Execute().Items;

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
								var delta = 3 - (DateTime.Now.Hour - DateTime.UtcNow.Hour); // 3 hour - for MSC time shift

								if (ev.Start.DateTime != null && ev.End.DateTime != null)
								{
									if (string.Format("{0:dd/MM/yy}", ev.Start.DateTime) == string.Format("{0:dd/MM/yy}", DateTime.UtcNow.AddHours(3))) { day = "сегодня"; }
									else if (string.Format("{0:dd/MM/yy}", ev.Start.DateTime) == string.Format("{0:dd/MM/yy}", DateTime.UtcNow.AddHours(3).AddDays(1))) { day = "завтра"; }
									else { day = string.Format("{0:dd/MM}", ev.Start.DateTime); }
									msg += $"\n[[LR]] {ev.Summary.Replace("'s leave request", string.Empty)} | {day} | {string.Format("{0:HH:mm}", ev.Start.DateTime.Value.AddHours(delta))} - {string.Format("{0:HH:mm}", ev.End.DateTime.Value.AddHours(delta))}";
								}
								else
								{
									var start = ev.Start.Date;
									var end = DateTime
										.ParseExact(ev.End.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
										.AddDays(-1)
										.ToString("yyyy-MM-dd");

									if (ev.Summary.Contains("'s sick leave"))
									{
										msg += $"\n[[SL]] {ev.Summary.Replace("'s sick leave", string.Empty)} |  {start} >>> {end}";
									}
									else if (ev.Summary.Contains("'s day off"))
									{
										msg += $"\n[[DO]] {ev.Summary.Replace("'s day off", string.Empty)} |  {start} >>> {end}";
									}
								}
							}
						}

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: msg,
							replyMarkup: new InlineKeyboardMarkup(_kbBuilder.ShowKeyboard()),
							parseMode: ParseMode.Markdown
						);
						break;

					case "/start":

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "Когда поставить leave request?",
							replyMarkup: new InlineKeyboardMarkup(_kbBuilder.DaysKeyboard())
							);
						break;

					case "/sick":

						var sickKeyboard = _kbBuilder.CalendarKeyboard(DateTime.UtcNow.AddHours(3), "sick_start");

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "С какого дня ты болеешь?", //Когда поставить сыкливого (sick leave)?",
							replyMarkup: new InlineKeyboardMarkup(sickKeyboard)
							);
						break;

					case "/dayoff":

						var dayoffKeyboard = _kbBuilder.CalendarKeyboard(DateTime.UtcNow.AddHours(3), "dayoff_start");

						await botClient.SendTextMessageAsync(
							chatId: e.Message.Chat,
							text: "С какого дня ты берёшь day off?",
							replyMarkup: new InlineKeyboardMarkup(dayoffKeyboard)
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
			if (!users.TryGetValue(e.Message.From.Id, out string v))
			{
				Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Received a text message from unauthorized user with id {e.Message.From}.");
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

				switch (Regex.Replace(e.CallbackQuery.Data, @"[\d-]", string.Empty))
				{
					case "today":
					case "tomorrow":
						await MessageDeletionOnCallback(e);

						var startHoursKeyboard = _kbBuilder.StartHoursKeyboard(e.CallbackQuery.Message.Chat.Id);

						await botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: $"Со скольки?",
							replyMarkup: new InlineKeyboardMarkup(startHoursKeyboard)
						);
						leaveRequestEvents[e.CallbackQuery.From.Id].Date = e.CallbackQuery.Data;
						break;

					case "sick_start":

						await MessageDeletionOnCallback(e);
						var sickStartTicks = long.Parse(e.CallbackQuery.Data.Substring(10));

						sickLeaveEvents[e.CallbackQuery.From.Id].Start = new DateTime(sickStartTicks);

						var sickEndKeyboard = _kbBuilder.CalendarKeyboard(DateTime.UtcNow.AddHours(3), "sick_end");

						await botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: "До какого дня поставить сыкливого (sick leave)?\nВыбери последний день больничного",
							replyMarkup: new InlineKeyboardMarkup(sickEndKeyboard)
							);
						break;

					case "dayoff_start":

						await MessageDeletionOnCallback(e);
						var dayoffStartTicks = long.Parse(e.CallbackQuery.Data.Substring(12));

						dayoffEvents[e.CallbackQuery.From.Id].Start = new DateTime(dayoffStartTicks);

						var dayoffEndKeyboard = _kbBuilder.CalendarKeyboard(DateTime.UtcNow.AddHours(3), "dayoff_end");

						await botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: "До какого дня тебя не будет?\nВыбери последний день day off",
							replyMarkup: new InlineKeyboardMarkup(dayoffEndKeyboard)
						);
						break;

					case "sick_end":

						await MessageDeletionOnCallback(e);
						var sickEndTicks = long.Parse(e.CallbackQuery.Data.Substring(8));

						sickLeaveEvents[e.CallbackQuery.From.Id].End = new DateTime(sickEndTicks);
						sickLeaveEvents[e.CallbackQuery.From.Id].Name = users[e.CallbackQuery.From.Id];

						var sickLeave = sickLeaveEvents[e.CallbackQuery.From.Id];


						var sickEvent = new Event
						{
							Summary = $"{sickLeave.Name}'s sick leave",
							Start = new EventDateTime
							{
								Date = string.Format("{0:yyyy-MM-dd}", sickLeave.Start)
							},
							End = new EventDateTime
							{
								Date = string.Format("{0:yyyy-MM-dd}", sickLeave.End.AddDays(1))
							},
							Description = "Event created using telegram bot"
						};

						if (sickLeave.Start <= sickLeave.End)
						{
							var recurringEvent = _service.Events.Insert(sickEvent, calendarId).Execute();

							await botClient.SendTextMessageAsync(
									chatId: e.CallbackQuery.From.Id,
									parseMode: ParseMode.Markdown,
									text: $"Cобытие *[{sickEvent.Summary}]* было успешно создано \nс *{sickLeave.Start.ToShortDateString()}* по *{sickLeave.End.ToShortDateString()}*\n\nПоставил случайно или ошибся? Можно удалить!",
									replyMarkup: new InlineKeyboardMarkup(_kbBuilder.DelOrConfirmSickKeyboard(recurringEvent.Id))
									);
						}
						else
						{
							SickLeaveEvent.Clear(sickLeaveEvents[e.CallbackQuery.From.Id]);
							await botClient.SendTextMessageAsync(
									chatId: e.CallbackQuery.From.Id,
									text: "Время окончания события указано раньше времени начала... попробуй ещё раз /start");
						}

						break;

					case "dayoff_end":

						await MessageDeletionOnCallback(e);
						var dayoffEndTicks = long.Parse(e.CallbackQuery.Data.Substring(10));

						dayoffEvents[e.CallbackQuery.From.Id].End = new DateTime(dayoffEndTicks);
						dayoffEvents[e.CallbackQuery.From.Id].Name = users[e.CallbackQuery.From.Id];

						var dayoff = dayoffEvents[e.CallbackQuery.From.Id];


						var dayoffEvent = new Event
						{
							Summary = $"{dayoff.Name}'s day off",
							Start = new EventDateTime
							{
								Date = string.Format("{0:yyyy-MM-dd}", dayoff.Start)
							},
							End = new EventDateTime
							{
								Date = string.Format("{0:yyyy-MM-dd}", dayoff.End.AddDays(1))
							},
							Description = "Event created using telegram bot"
						};

						if (dayoff.Start <= dayoff.End)
						{
							var recurringEvent = _service.Events.Insert(dayoffEvent, calendarId).Execute();

							await botClient.SendTextMessageAsync(
								chatId: e.CallbackQuery.From.Id,
								parseMode: ParseMode.Markdown,
								text: $"Cобытие *[{dayoffEvent.Summary}]* было успешно создано \nс *{dayoff.Start.ToShortDateString()}* по *{dayoff.End.ToShortDateString()}*\n\nПоставил случайно или ошибся? Можно удалить!",
								replyMarkup: new InlineKeyboardMarkup(_kbBuilder.DelOrConfirmSickKeyboard(recurringEvent.Id))
							);
						}
						else
						{
							SickLeaveEvent.Clear(dayoffEvents[e.CallbackQuery.From.Id]);
							await botClient.SendTextMessageAsync(
								chatId: e.CallbackQuery.From.Id,
								text: "Время окончания события указано раньше времени начала... попробуй ещё раз /start");
						}

						break;

					case "start":
						//0-16
						await MessageDeletionOnCallback(e);

						var endHoursKeyboard = _kbBuilder.EndHoursKeyboard(e.CallbackQuery.Message.Chat.Id);

						await botClient.SendTextMessageAsync(
							chatId: e.CallbackQuery.From.Id,
							text: "И до скольки?",
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

						var nowMsc = DateTime.UtcNow.AddHours(3);

						var parts = starts[e.CallbackQuery.From.Id].Split('.');
						var startHour = int.Parse(parts[0]);
						var startMin = int.Parse(parts[1]);

						var workdayBeginningUtc = new DateTime(
							nowMsc.Year,
							nowMsc.Month,
							nowMsc.Day,
							startHour - 3,
							startMin,
							0,
							DateTimeKind.Utc);

						if (leaveRequestEvents[e.CallbackQuery.From.Id].Date == "tomorrow")
						{
							workdayBeginningUtc = workdayBeginningUtc.AddDays(1);
						}

						var start = workdayBeginningUtc.AddMinutes(leaveRequestEvents[e.CallbackQuery.From.Id].Start * TimeButtonsDiff);
						var end = workdayBeginningUtc.AddMinutes(leaveRequestEvents[e.CallbackQuery.From.Id].End * TimeButtonsDiff);

						var lrEvent = new Event
						{
							Summary = $"{leaveRequestEvents[e.CallbackQuery.From.Id].Name}'s leave request",
							//Location = "Somewhere",
							Start = new EventDateTime
							{
								DateTime = start,
								TimeZone = "Europe/Moscow"
							},
							End = new EventDateTime
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
								var recurringEvent = await _service.Events.Insert(lrEvent, calendarId).ExecuteAsync();

								var from = $"{start.AddHours(3):HH:mm}";
								var to = $"{end.AddHours(3):HH:mm}";

								var day = (DateTime.UtcNow.AddHours(3).Date == lrEvent.Start.DateTime.Value.Date) ? "Сегодня" : "Завтра";
								await botClient.SendTextMessageAsync(
										chatId: e.CallbackQuery.From.Id,
										parseMode: ParseMode.Markdown,
										text: $"Cобытие *[{lrEvent.Summary}]* было успешно создано \n{day}, с *{from}* до *{to}*\n\nПоставил случайно или ошибся? Можно удалить!",
										replyMarkup: new InlineKeyboardMarkup(_kbBuilder.DelOrConfirmLRKeyboard(recurringEvent.Id))
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
								_service.Events.Delete(calendarId, eventId).Execute();
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
					Console.WriteLine(ex.ErrorCode == 400
						? $"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Message with id {e.CallbackQuery.Message.MessageId} has already been deleted"
						: $"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Exception :( StackTrace: {ex.StackTrace}");
				}
			}
			else
			{
				await botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id, "удоли через контекстное меню");
				Console.WriteLine($"{string.Format("{0:[HH:mm:ss] dd.MM.yy}", DateTime.Now)} - Old message deletion attempt {e.CallbackQuery.Message.Date} in chat with {users[e.CallbackQuery.From.Id]}");
				LeaveRequestEvent.Clear(leaveRequestEvents[e.CallbackQuery.From.Id]);
			}
		}
	}
}