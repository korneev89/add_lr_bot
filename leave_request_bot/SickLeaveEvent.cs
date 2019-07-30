using System;
using System.Collections.Generic;
using System.Text;

namespace TlgrmBot
{
	public class SickLeaveEvent
	{
		public string Name { get; set; }
		public string Date { get; set; }
		public DateTime Start { get; set; }
		public DateTime End { get; set; }

		public SickLeaveEvent()
		{
			Name = "";
			Date = "";
		}

		static public void Clear(SickLeaveEvent ev)
		{
			ev.Name = "";
			ev.Date = "";
			ev.Start = new DateTime(1900, 1, 1);
			ev.End = new DateTime(1900, 1, 1);
        }
	}
}