using System.Web.Http;
using System.Web.Http.Dispatcher;
using PCFC.DocGen.Api.Filters;

namespace PCFC.DocGen.Api.App_Start
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Guard against stale/duplicate assemblies by allowing only current namespace controllers.
            config.Services.Replace(typeof(IHttpControllerTypeResolver), new NamespaceFilteredHttpControllerTypeResolver());
            config.Filters.Add(new ApiExceptionFilter());

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional });
        }
    }
}
