using System;
using Newtonsoft.Json;

namespace CrashReports.Models
{
	public class ReportModel
	{
		public int Id { get; set; }
		public string Title { get; set; }
		public string Details { get; set; }
		public DateTime Created { get; set; }
		public int Occurences { get; set; }
		public DateTime LastCrash { get; set; }
		public string ApplicationName { get; set; }
		public int UserId { get; set; }
		public string AppVersion { get; set; }
		public string OperatingSystem { get; set; }

		[JsonIgnore]
		public Version ApplicationVersion
		{
			get
			{
				return m_applicationVersion ??
					(m_applicationVersion = new Version(string.IsNullOrWhiteSpace(AppVersion) ? "1.00" : AppVersion));
			}
		}
		Version m_applicationVersion;
	}
}