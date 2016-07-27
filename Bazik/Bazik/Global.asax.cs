using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Bazik
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute("IndexRanger", "d/{serverName}", new { controller = "IndexRanger",
                action = "ShowDashboard", serverName = UrlParameter.Optional });
            routes.MapRoute("IndexRanger-request", "request", new { controller = "IndexRanger", action = "Request" });
            routes.MapRoute("IndexRanger-index", "index-stats", new { controller = "IndexRanger", action = "IndexStats" });
            routes.MapRoute("IndexRanger-top", "top", new { controller = "IndexRanger", action = "TopQueries" });

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}", // URL with parameters
                new { controller = "IndexRanger", action = "ShowDashboard" } // Parameter defaults
            );
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);
        }
    }
}
