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
using CrashReports.Models;

namespace CrashReports.Controllers
{
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
					.Where(x => !x.Fixed && !x.Ignore && !x.Deleted)
					.OrderByDescending(x => x.Created)
					.Select(x => new ReportModel
					{
						Id = x.ReportId,
						Created = x.Created,
						Title = x.Title,
						Occurences = x.Occurences,
						LastCrash = x.LastCrash.GetValueOrDefault(x.Created)
					}).ToList();

				model.IgnoredCount = context.Reports.Count(x => x.Ignore);
				model.FixedCount = context.Reports.Count(x => x.Fixed);
			}
			return View(model);
		}

		[Authorize]
		public ActionResult Log(int id)
		{
			ReportModel model = new ReportModel();
			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				var result = context.Reports.FirstOrDefault(x => x.ReportId == id);
				if (result != null)
				{
					model.Id = result.ReportId;
					model.Created = result.Created;
					model.Title = result.Title;
					model.Details = result.Details;
					model.Occurences = result.Occurences;
					model.LastCrash = result.LastCrash.GetValueOrDefault(result.Created);
				}
			}

			return View(model);
		}

		[HttpPost]
		public ActionResult CompressedLog()
		{
			byte[] byteData;
			using (Stream stream = Request.GetBufferedInputStream())
			using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress))
			using (MemoryStream memStream = new MemoryStream())
			{
				gZipStream.CopyTo(memStream);
				byteData = memStream.ToArray();
			}

			string jsonData = Encoding.UTF8.GetString(byteData);
			ReportModel data = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportModel>(jsonData);

			CaptureLogData(data);

			return new HttpStatusCodeResult(HttpStatusCode.Accepted);
		}

		private void CaptureLogData(ReportModel data)
		{
			string uniqueId = GetUniqueId(data.Title + data.Details);

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
						Occurences = 1
					});
				else
				{
					previousReport.LastCrash = DateTime.UtcNow;
					previousReport.Occurences++;
				}

				context.SubmitChanges(ConflictMode.ContinueOnConflict);
			}
		}

		[HttpPost]
		public ActionResult Log(string errorMessage, string details, string appName = "Smores", int userId = 1)
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

			CaptureLogData(new ReportModel { ApplicationName = appName, Details = details, Title = errorMessage, UserId = userId });

			return new HttpStatusCodeResult(HttpStatusCode.Accepted);
		}

		[Authorize]
		[HttpPost]
		public ActionResult Delete(int id)
		{
			if (!User.IsInRole("Admin"))
				return View("Error");

			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report report = context.Reports.FirstOrDefault(x => x.ReportId == id);
				if (report != null)
				{
					report.Deleted = true;
					report.Details = "";
					context.SubmitChanges(ConflictMode.ContinueOnConflict);
				}
			}

			TempData["SystemMessage"] = "Deleted crash log";
			return RedirectToAction("Index");
		}

		[Authorize]
		[HttpPost]
		public ActionResult MarkFixed(int id)
		{
			if (!User.IsInRole("Admin"))
				return View("Error");

			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report report = context.Reports.FirstOrDefault(x => x.ReportId == id);
				if (report != null)
				{
					report.Fixed = true;
					report.Details = "";
					context.SubmitChanges(ConflictMode.ContinueOnConflict);
				}
			}

			TempData["SystemMessage"] = "crash log";
			return RedirectToAction("Index");
		}

		[Authorize]
		[HttpPost]
		public ActionResult Ignore(int id)
		{
			if (!User.IsInRole("Admin"))
				return View("Error");

			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report report = context.Reports.FirstOrDefault(x => x.ReportId == id);
				if (report != null)
				{
					report.Ignore = true;
					report.Details = "";
					context.SubmitChanges(ConflictMode.ContinueOnConflict);
				}
			}

			TempData["SystemMessage"] = "Crash marked as ignored";
			return RedirectToAction("Index");
		}

		private string GetUniqueId(string report)
		{
			using (MD5 hash = MD5.Create())
			{
				byte[] data = hash.ComputeHash(Encoding.UTF8.GetBytes(report));
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
