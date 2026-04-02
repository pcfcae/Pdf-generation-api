using System.Web;
using System.Web.Http;
using PCFC.DocGen.Api.Config;

namespace PCFC.DocGen.Api
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(App_Start.WebApiConfig.Register);
            SwaggerConfig.Register(GlobalConfiguration.Configuration);
        }

        protected void Application_BeginRequest()
        {
            var isRootRequest =
                string.Equals(Request.HttpMethod, "GET", System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Request.AppRelativeCurrentExecutionFilePath, "~/", System.StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(Request.PathInfo);

            if (!isRootRequest)
            {
                return;
            }

            Response.Redirect("~/swagger/ui/index", true);
        }
    }
}
