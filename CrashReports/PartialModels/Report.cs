using System;

namespace CrashReports
{
	public partial class Report
	{
		public string[] FixedInVersions { get { return FixedInVersion.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries); } }
		public string[] AppVersions { get { return AppVersion.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries); } }

		public string AddFixedVersion(string version)
		{
			return FixedInVersion += version + ";";
		}

		public string AddAppVersion(string version)
		{
			return AppVersion += ";" + version;
		}
	}
}