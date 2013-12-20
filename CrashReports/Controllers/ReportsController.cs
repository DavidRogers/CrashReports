using System;
using System.Configuration;
using System.Data.Linq;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using CrashReports.Filters;
using CrashReports.Models;

namespace CrashReports.Controllers
{
	[InitializeSimpleMembership]
	public class ReportsController : Controller
	{
		[Authorize]
		public ActionResult Index()
		{
			FullReportViewModel model = new FullReportViewModel();
			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				model.Reports = context
					.Reports
					.Where(x => !x.Fixed && !x.Ignore && !x.Deleted && x.LastCrash > DateTime.UtcNow.Subtract(TimeSpan.FromDays(14)))
					.OrderByDescending(x => x.Created)
					.Select(x => new ReportModel
					{
						Id = x.ReportId,
						Created = x.Created,
						Title = x.Title,
						ApplicationName = x.AppName,
						AppVersion = x.AppVersion,
						Occurences = x.Occurences,
						Fixed = x.Fixed,
						FixedInVersion = x.FixedInVersion,
						LastCrash = x.LastCrash.GetValueOrDefault(x.Created)
					})
					.ToList();

				model.Applications = context
					.Reports
					.Select(x => x.AppName)
					.Distinct()
					.ToList();
			}
			return View(model);
		}

		[Authorize]
		public ActionResult Application(string appName)
		{
			FullReportViewModel model = new FullReportViewModel();
			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				model.Reports = context
					.Reports
					.Where(x => !x.Fixed && !x.Ignore && !x.Deleted && x.AppName == appName)
					.OrderByDescending(x => x.Created)
					.Select(x => new ReportModel
					{
						Id = x.ReportId,
						Created = x.Created,
						Title = x.Title,
						ApplicationName = x.AppName,
						AppVersion = x.AppVersion,
						Occurences = x.Occurences,
						LastCrash = x.LastCrash.GetValueOrDefault(x.Created)
					}).ToList();

				model.IgnoredCount = context.Reports.Where(x => x.AppName == appName).Count(x => x.Ignore);
				model.FixedCount = context.Reports.Where(x => x.AppName == appName).Count(x => x.Fixed);
			}
			return View(model);
		}

		[Authorize]
		public ActionResult Log(int id, string version)
		{
			ReportModel model = new ReportModel();
			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report result = context.Reports.FirstOrDefault(x => x.ReportId == id);
				if (result != null)
				{
					model.Id = result.ReportId;
					model.Created = result.Created;
					model.Title = result.Title;
					model.Details = result.Details;
					model.AppVersion = version;
					model.ApplicationName = result.AppName;
					model.Occurences = result.Occurences;
					model.LastCrash = result.LastCrash.GetValueOrDefault(result.Created);
					model.OperatingSystem = result.OperatingSystem;
				}
			}

			return View(model);
		}

		[HttpPost]
		public ActionResult CompressedLog()
		{
			////byte[] byteData;
			////using (Stream stream = Request.GetBufferedInputStream())
			////using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress))
			////using (MemoryStream memStream = new MemoryStream())
			////{
			////	gZipStream.CopyTo(memStream);
			////	byteData = memStream.ToArray();
			////}

			////string jsonData = Encoding.UTF8.GetString(byteData);
			string content;
			using (Stream input = Request.GetBufferlessInputStream())
			using (StreamReader reader = new StreamReader(input))
			{
				content = reader.ReadToEnd();
			}
			ReportModel data = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportModel>(content);

			CaptureLogData(data);

			return new HttpStatusCodeResult(HttpStatusCode.Accepted);
		}

		private void CaptureLogData(ReportModel data)
		{
			string uniqueId = GetUniqueId(data.Title, data.Details);

			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report previousReport = context.Reports.FirstOrDefault(x => x.UniqueId == uniqueId);

				if (previousReport == null)
					context.Reports.InsertOnSubmit(new Report
					{
						Title = data.Title,
						Details = data.Details,
						AppName = data.ApplicationName,
						UserId = data.UserId,
						Created = DateTime.UtcNow,
						LastCrash = DateTime.UtcNow,
						UniqueId = uniqueId,
						AppVersion = data.AppVersion,
						Occurences = 1,
						OperatingSystem = data.OperatingSystem
					});
				else
				{
					if (previousReport.Deleted || previousReport.Ignore)
						return;

					if (!previousReport.AppVersions.Contains(data.AppVersion))
					{
						previousReport.AppVersion = previousReport.AddAppVersion(data.AppVersion);
						previousReport.Fixed = false;
						previousReport.Details = data.Details;
					}
					previousReport.LastCrash = DateTime.UtcNow;
					previousReport.Occurences++;
				}

				context.SubmitChanges(ConflictMode.ContinueOnConflict);
			}
		}

		[HttpPost]
		public ActionResult Log(string errorMessage, string details, string appName = "Smores", string appVersion = "1.0.0", int userId = 1)
		{
			// trim input to enforce length restraints
			if (string.IsNullOrWhiteSpace(errorMessage) || string.IsNullOrWhiteSpace(details))
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

			errorMessage = errorMessage.Trim();
			if (errorMessage.Length > 200)
				errorMessage = errorMessage.Substring(0, 200);

			details = Encoding.UTF8.GetString(Convert.FromBase64String(details));
			if (details.Length > 10000)
				details = details.Substring(details.Length - 10000, 10000);

			CaptureLogData(new ReportModel { ApplicationName = appName, Details = details, Title = errorMessage, AppVersion = appVersion, UserId = userId });

			return new HttpStatusCodeResult(HttpStatusCode.Accepted);
		}

		[Authorize]
		[HttpPost]
		public ActionResult Delete(int id)
		{
			if (!User.IsInRole("Admin"))
				return View("Error");

			string appName = null;
			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report report = context.Reports.First(x => x.ReportId == id);
				if (report != null)
				{
					appName = report.AppName;
					context.Reports.DeleteOnSubmit(report);
					context.SubmitChanges(ConflictMode.ContinueOnConflict);
				}
			}

			TempData["SystemMessage"] = "Deleted crash log";
			return RedirectToAction("Application", new { appName });
		}

		[Authorize]
		[HttpPost]
		public ActionResult MarkFixed(int id, string version)
		{
			if (!User.IsInRole("Admin"))
				return View("Error");

			string appName = null;
			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report report = context.Reports.First(x => x.ReportId == id);
				report.Fixed = true;
				report.FixedInVersion = report.AddFixedVersion(version);
				report.Details = "";
				appName = report.AppName;
				context.SubmitChanges(ConflictMode.ContinueOnConflict);
			}

			TempData["SystemMessage"] = "crash log";
			return RedirectToAction("Application", new { appName });
		}

		[Authorize]
		[HttpPost]
		public ActionResult Ignore(int id)
		{
			if (!User.IsInRole("Admin"))
				return View("Error");

			string appName = null;
			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report report = context.Reports.FirstOrDefault(x => x.ReportId == id);
				if (report != null)
				{
					report.Ignore = true;
					report.Details = "";
					appName = report.AppName;
					context.SubmitChanges(ConflictMode.ContinueOnConflict);
				}
			}

			TempData["SystemMessage"] = "Crash marked as ignored";
			return RedirectToAction("Application", new { appName });
		}

		private string GetUniqueId(string message, string stackTrace)
		{
			string shortTrace = string.Join("\r\n", stackTrace.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).Take(3));
			using (MD5 hash = MD5.Create())
			{
				byte[] data = hash.ComputeHash(Encoding.UTF8.GetBytes(message + shortTrace));
				StringBuilder sBuilder = new StringBuilder();

				// Loop through each byte of the hashed data  
				// and format each one as a hexadecimal string. 
				for (int i = 0; i < data.Length; i++)
				{
					sBuilder.Append(data[i].ToString("x2"));
				}

				// Return the hexadecimal string. 
				return sBuilder.ToString();
			}
		}
	}
}
