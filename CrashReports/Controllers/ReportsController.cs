using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Linq;
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
						ErrorMessage = x.Title,
						Occurences = x.Occurences,
					}).ToList();
				model.IgnoredCount = context.Reports.Count(x => x.Ignore);
				model.FixedCount = context.Reports.Count(x => x.Fixed);
			}
			return View(model);
		}

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
					model.ErrorMessage = result.Title;
					model.StackTrace = result.Details;
					model.Occurences = result.Occurences;
				}
			}

			return View(model);
		}

		[HttpPost]
		public ActionResult Log(string errorMessage, string details)
		{
			// trim input to enforce length restraints
			if (string.IsNullOrWhiteSpace(errorMessage) || string.IsNullOrWhiteSpace(details))
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

			errorMessage = errorMessage.Trim();
			if (errorMessage.Length > 200)
				errorMessage = errorMessage.Substring(0, 200);
			if (details.Length > 10000)
				details = details.Substring(details.Length - 10000, 10000);

			string uniqueId = GetUniqueId(errorMessage + details);

			using (CrashReportsDataContext context = new CrashReportsDataContext(ConfigurationManager.AppSettings["SQLSERVER_CONNECTION_STRING"]))
			{
				Report previousReport = context.Reports.FirstOrDefault(x => x.UniqueId == uniqueId);

				if (previousReport == null)
					context.Reports.InsertOnSubmit(new Report
					{
						Title = errorMessage,
						Details = details.Trim(),
						Created = DateTime.UtcNow,
						UniqueId = uniqueId,
						Occurences = 1
					});
				else
					previousReport.Occurences++;

				context.SubmitChanges(ConflictMode.ContinueOnConflict);
			}

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

			return RedirectToAction("Log", new { id });
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

			return RedirectToAction("Log", new { id });
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
