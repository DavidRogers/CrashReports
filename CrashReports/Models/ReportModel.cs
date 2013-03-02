using System;

namespace CrashReports.Models
{
	public class ReportModel
	{
		public int Id { get; set; }
		public string ErrorMessage { get; set; }
		public string StackTrace { get; set; }
		public DateTime Created { get; set; }
		public int Occurences { get; set; }
	}
}