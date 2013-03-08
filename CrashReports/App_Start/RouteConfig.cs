using System.Web.Mvc;
using System.Web.Routing;

namespace CrashReports
{
	public class RouteConfig
	{
		public static void RegisterRoutes(RouteCollection routes)
		{
			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

			routes.MapRoute(
				name: "CompressedLog",
				url: "reports/log/compressed",
				defaults: new { controller = "Reports", action = "CompressedLog" }
			);

			routes.MapRoute(
				name: "Default",
				url: "{controller}/{action}/{id}",
				defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
			);
		}
	}
}