using System.Collections.Generic;

namespace CrashReports.Models
{
	public class FullReportViewModel
	{
		public IEnumerable<ReportModel> Reports { get; set; }
		public int FixedCount { get; set; }
		public int IgnoredCount { get; set; }
	}
}