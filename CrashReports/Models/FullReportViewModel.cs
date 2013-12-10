using System.Collections.Generic;

namespace CrashReports.Models
{
	public class FullReportViewModel
	{
		public List<string> Applications { get; set; }
		public IEnumerable<ReportModel> Reports { get; set; }
		public int FixedCount { get; set; }
		public int IgnoredCount { get; set; }
	}
}
