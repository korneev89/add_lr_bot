using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;

namespace LeaveRequestsBot
{
	public class KeyboardBuilder
	{
		private int NumberOfTimeButtons { get; }
		private int TimeButtonsDiff { get; }
		private Dictionary<long, string> Starts { get; }

		public KeyboardBuilder(int numberOfTimeButtons, int timeButtonsDiff, Dictionary<long, string> starts)
		{
			NumberOfTimeButtons = numberOfTimeButtons;
			TimeButtonsDiff = timeButtonsDiff;
			Starts = starts;
		}

		public List<List<InlineKeyboardButton>> StartHoursKeyboard(long chatId)
		{
			var keyboard = new List<List<InlineKeyboardButton>>();

			var startRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData(
					GetTimeString(chatId, 0),
					"start0")
			};

			var cancelStartHoursRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData(
					"❌ Отмена",
					"cancel_start_hours")
			};

			keyboard.Add(startRow);

			for (int i = 0; i < 9; i++)
			{
				keyboard.Add(CreateStartButtonRow(chatId, i));
			}
			keyboard.Add(cancelStartHoursRow);

			return keyboard;
		}

		public List<List<InlineKeyboardButton>> EndHoursKeyboard(long chatId)
		{
			var keyboard = new List<List<InlineKeyboardButton>>();

			for (int i = 0; i < 9; i++)
			{
				keyboard.Add(CreateEndButtonRow(chatId, i));
			}

			var cancelEndHoursRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData(
					"❌ Отмена",
					"cancel_end_hours")
			};

			keyboard.Add(cancelEndHoursRow);

			return keyboard;
		}

		public List<List<InlineKeyboardButton>> ShowKeyboard()
		{
			var okShowButtonRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData("✅ ОK", "ok_show")
			};

			return new List<List<InlineKeyboardButton>>
			{
				okShowButtonRow
			};
		}

		public List<List<InlineKeyboardButton>> HostKeyboard()
		{
			var okHostButtonRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData("✅ ОK", "ok_host")
			};

			return new List<List<InlineKeyboardButton>>
			{
				okHostButtonRow
			};
		}

		public List<List<InlineKeyboardButton>> DaysKeyboard()
		{
			var daysButtonRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData("сегодня", "today"),
				InlineKeyboardButton.WithCallbackData("завтра", "tomorrow")
			};

			var cancelDaysButtonRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_days")
			};

			return new List<List<InlineKeyboardButton>>
			{
				daysButtonRow,
				cancelDaysButtonRow
			};
		}

		public List<List<InlineKeyboardButton>> DelOrConfirmKeyboard(string recurringEventId, string eventName)
		{
			var delOrConfirmRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData("✅ Оставить событие", $"confirm_{eventName}"),
				InlineKeyboardButton.WithCallbackData("❌ Удалить событие", $"delete{recurringEventId}")
			};

			return new List<List<InlineKeyboardButton>>
			{
				delOrConfirmRow
			};
		}

		public List<List<InlineKeyboardButton>> CalendarKeyboard(DateTime date, string target)
		{
			string year = date.Year.ToString();
			string month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.Month);
			DateTime firstDay = new DateTime(date.Year, date.Month, 1);
			int daysInCurrentMonth = DateTime.DaysInMonth(date.Year, date.Month);
			DateTime lastDay = new DateTime(date.Year, date.Month, daysInCurrentMonth);

			int dayOfWeekFirst = ((int)firstDay.DayOfWeek > 0) ? (int)firstDay.DayOfWeek : 7;
			int dayOfWeekLast = ((int)lastDay.DayOfWeek > 0) ? (int)lastDay.DayOfWeek : 7;
			int rowsCount = (dayOfWeekFirst - 1 + daysInCurrentMonth + (7 - dayOfWeekLast)) / 7;

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

			List<List<InlineKeyboardButton>> keyboard = new List<List<InlineKeyboardButton>>
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
				keyboard.Add(daysRow);
			}

			keyboard.Add(calendarNavigationRow);

			return keyboard;
		}

		public List<List<InlineKeyboardButton>> DeletionKeyboard(MessageEventArgs e, string calendarId, CalendarService service, Dictionary<int, string> users)
		{
			var deletionKeyboard = new List<List<InlineKeyboardButton>>();

			var leaveRequests = service.Events.List(calendarId);
			leaveRequests.TimeMin = DateTime.UtcNow.Date;
			var allLeaveRequest = leaveRequests.Execute().Items;

			if (allLeaveRequest.Count > 0)
			{
				IList<Event> userLeaveRequests = allLeaveRequest
					.Where(ev =>
						ev.Creator?.Email == "dmitri.korneev@cbsinteractive.com" &&
						ev.Summary == $"{users[e.Message.From.Id]}'s leave request")
					.OrderBy(ev => ev.Start.DateTime)
					.ThenBy(ev => ev.End.DateTime)
					.ToList();

				if (userLeaveRequests.Count > 0)
				{
					foreach (var ev in userLeaveRequests)
					{
						string day;
						var delta = 3 - (DateTime.Now.Hour - DateTime.UtcNow.Hour); // 3 hour - for MSC time shift

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

						var buttonRow = new List<InlineKeyboardButton>
						{
							InlineKeyboardButton.WithCallbackData(
								$"[LR] {day} | {string.Format("{0:HH:mm}", ev.Start.DateTime.Value.AddHours(delta))} - {string.Format("{0:HH:mm}", ev.End.DateTime.Value.AddHours(delta))}",
								$"delete{ev.Id}")
						};

						deletionKeyboard.Add(buttonRow);
					}
				}
			}

			var events = service.Events.List(calendarId);
			events.TimeMin = DateTime.UtcNow.Date.AddDays(-14);
			var allEvents = events.Execute().Items;

			if (allEvents.Count > 0)
			{
				IList<Event> userDayoffs = allEvents.Where(
						ev => ev.Creator?.Email == "dmitri.korneev@cbsinteractive.com"
						      && ev.Summary == $"{users[e.Message.From.Id]}'s day off")
					.OrderBy(ev => ev.Start.Date)
					.ThenBy(ev => ev.End.Date)
					.ToList();

				if (userDayoffs.Count > 0)
				{
					deletionKeyboard.AddRange(
						userDayoffs
							.Select(ev =>
							{
								var end = DateTime
									.ParseExact(ev.End.Date, "yyyy-MM-dd",
										System.Globalization.CultureInfo.InvariantCulture)
									.AddDays(-1)
									.ToString("yyyy-MM-dd");

								return new List<InlineKeyboardButton>
								{
									InlineKeyboardButton.WithCallbackData(
										$"[DO] {ev.Start.Date} >>> {end}",
										$"delete{ev.Id}")
								};
							}));
				}

				IList<Event> userSickLeaves = allEvents.Where(
						ev => ev.Creator?.Email == "dmitri.korneev@cbsinteractive.com"
							&& ev.Summary == $"{users[e.Message.From.Id]}'s sick leave")
					.OrderBy(ev => ev.Start.Date)
					.ThenBy(ev => ev.End.Date)
					.ToList();

				if (userSickLeaves.Count > 0)
				{
					deletionKeyboard.AddRange(
						userSickLeaves
							.Select(ev =>
							{
								var end = DateTime
									.ParseExact(ev.End.Date, "yyyy-MM-dd",
										System.Globalization.CultureInfo.InvariantCulture)
									.AddDays(-1)
									.ToString("yyyy-MM-dd");

								return new List<InlineKeyboardButton>
								{
									InlineKeyboardButton.WithCallbackData(
										$"[SL] {ev.Start.Date} >>> {end}",
										$"delete{ev.Id}")
								};
							}));
				}
				IList<Event> userVacations = allEvents.Where(
						ev => ev.Creator?.Email == "dmitri.korneev@cbsinteractive.com"
						      && ev.Summary == $"{users[e.Message.From.Id]}'s vacation")
					.OrderBy(ev => ev.Start.Date)
					.ThenBy(ev => ev.End.Date)
					.ToList();

				if (userVacations.Count > 0)
				{
					deletionKeyboard.AddRange(
						userVacations
							.Select(ev =>
							{
								var end = DateTime
									.ParseExact(ev.End.Date, "yyyy-MM-dd",
										System.Globalization.CultureInfo.InvariantCulture)
									.AddDays(-1)
									.ToString("yyyy-MM-dd");

								return new List<InlineKeyboardButton>
								{
									InlineKeyboardButton.WithCallbackData(
										$"[PTO] {ev.Start.Date} >>> {end}",
										$"delete{ev.Id}")
								};
							}));
				}
			}

			var okDeletionButtonRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData("✅ ОK", "ok_deletion")
			};

			var cancelDeletionButtonRow = new List<InlineKeyboardButton>
			{
				InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel_deletion")
			};

		deletionKeyboard.Add(deletionKeyboard.Count > 0 ? cancelDeletionButtonRow : okDeletionButtonRow);

			return deletionKeyboard;
		}

		private List<InlineKeyboardButton> CreateStartButtonRow(long chatId, int rowId)
		{
			var row = new List<InlineKeyboardButton>();
			for (var i = 1; i < 7; i++)
			{
				row.Add(InlineKeyboardButton.WithCallbackData(
					GetTimeString(chatId, rowId * NumberOfTimeButtons + i),
					"start" + (rowId * NumberOfTimeButtons + i)));
			}

			return row;
		}

		private List<InlineKeyboardButton> CreateEndButtonRow(long chatId, int rowId)
		{
			var row = new List<InlineKeyboardButton>();
			for (var i = 1; i < 7; i++)
			{
				row.Add(InlineKeyboardButton.WithCallbackData(
					GetTimeString(chatId, rowId * NumberOfTimeButtons + i),
					"end" + (rowId * NumberOfTimeButtons + i)));
			}

			return row;
		}

		private string GetTimeString(long chatId, int delta)
		{
			var startTimeParts = Starts[chatId].Split('.');
			var startHour = int.Parse(startTimeParts[0]);
			var startMin = int.Parse(startTimeParts[1]);

			var dateTime = new DateTime(1900, 1, 1, startHour, startMin, 0)
				.AddMinutes(TimeButtonsDiff * delta);

			return string.Join(":", dateTime.ToString("HH"), dateTime.ToString("mm"));
		}
	}
}
