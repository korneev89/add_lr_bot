namespace LeaveRequestsBot
{
	public class LeaveRequestEvent
	{
		public string Name { get; set; }
		public string Date { get; set; }
		public int Start { get; set; }
		public int End { get; set; }

		public LeaveRequestEvent()
		{
			Name = "";
			Date = "";
			Start = 0;
			End = 0;
		}

		static public void Clear(LeaveRequestEvent ev)
		{
			ev.Name = "";
			ev.Date = "";
			ev.Start = 0;
			ev.End = 0;
		}
	}
}