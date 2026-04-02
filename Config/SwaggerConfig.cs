using System.Web.Http;
using Swashbuckle.Application;
using System.Reflection;

namespace PCFC.DocGen.Api.Config
{
    public static class SwaggerConfig
    {
        public static void Register(HttpConfiguration config)
        {
            var thisAssembly = Assembly.GetExecutingAssembly();
            config
                .EnableSwagger(c =>
                {
                    c.SingleApiVersion("v1", "PCFC.DocGen.Api");
                    c.PrettyPrint();
                })
                .EnableSwaggerUi(c =>
                {
                    c.InjectJavaScript(thisAssembly, "PCFC.DocGen.Api.Swagger.response-linkify.js");
                });
        }
    }
}
