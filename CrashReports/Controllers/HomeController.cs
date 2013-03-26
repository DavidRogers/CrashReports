using System.Web.Mvc;
using CrashReports.Filters;

namespace CrashReports.Controllers
{
	[InitializeSimpleMembership]
	public class HomeController : Controller
	{
		public ActionResult Index()
		{
			return View();
		}
	}
}
