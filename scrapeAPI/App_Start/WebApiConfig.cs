using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using dsmodels;
using Microsoft.Owin.Security.OAuth;
using Newtonsoft.Json.Serialization;
using Unity;
using Unity.Lifetime;

namespace scrapeAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            // Configure Web API to use only bearer token authentication.
            config.SuppressDefaultHostAuthentication();
            config.Filters.Add(new HostAuthenticationFilter(OAuthDefaults.AuthenticationType));

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            //https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/dependency-injection
            var container = new UnityContainer();
            container.RegisterType<IRepository, Repository>(new HierarchicalLifetimeManager());
            config.DependencyResolver = new UnityResolver(container);
        }
    }
}
