using System;
using System.Collections.Generic;
using System.Text;

namespace TlgrmBot
{
	public class CalendarEvent
	{
		public string Name { get; set; }
		public string Date { get; set; }
		public int Start { get; set; }
		public int End { get; set; }
		public int FromTlgrmID { get; set; }

		public CalendarEvent()
		{
			Name = "";
			Date = "";
			Start = 0;
			End = 0;
		}

		static public void Clear(CalendarEvent ev)
		{
			ev.Name = "";
			ev.Date = "";
			ev.Start = 0;
			ev.End = 0;
		}
	}
}