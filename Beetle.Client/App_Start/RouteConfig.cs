﻿using System.Web.Mvc;
using System.Web.Routing;

namespace Beetle.Client.App_Start {

    public class RouteConfig {

        public static void RegisterRoutes(RouteCollection routes) {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("Services/{resource}.svc/{*pathInfo}");

            routes.MapRoute("Default", "{controller}/{action}/{id}", 
                            new { controller = "Home", action = "Index", id = UrlParameter.Optional });
        }
    }
}