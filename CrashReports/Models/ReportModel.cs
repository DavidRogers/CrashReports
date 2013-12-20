using System;
using System.Linq;
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
		public string FixedInVersion { get; set; }
		public bool Fixed { get; set; }

		[JsonIgnore]
		public Version[] FixedInVersions { get { return (FixedInVersion ?? "").Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(x => new Version(x)).ToArray(); } }
		[JsonIgnore]
		public Version[] AppVersions { get { return (AppVersion ?? "").Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(x => new Version(x)).ToArray(); } }

		[JsonIgnore]
		public Version ApplicationVersion
		{
			get
			{
				return m_applicationVersion ??
					(m_applicationVersion = string.IsNullOrWhiteSpace(AppVersion) ? new Version("1.00") : AppVersions.Max());
			}
		}
		Version m_applicationVersion;
	}
}