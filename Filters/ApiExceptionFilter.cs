using System;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;

namespace PCFC.DocGen.Api.Filters
{
    public class ApiExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            var message = context.Exception == null ? "Unknown API error." : context.Exception.Message;
            context.Response = context.Request.CreateResponse(HttpStatusCode.InternalServerError, new
            {
                Message = message,
                ExceptionType = context.Exception == null ? string.Empty : context.Exception.GetType().FullName
            });
        }
    }
}
