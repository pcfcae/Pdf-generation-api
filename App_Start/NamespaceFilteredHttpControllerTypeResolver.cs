using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Dispatcher;

namespace PCFC.DocGen.Api.App_Start
{
    internal sealed class NamespaceFilteredHttpControllerTypeResolver : IHttpControllerTypeResolver
    {
        private const string AllowedNamespacePrefix = "PCFC.DocGen.Api.Controllers";
        private static readonly DefaultHttpControllerTypeResolver DefaultResolver = new DefaultHttpControllerTypeResolver();

        public ICollection<Type> GetControllerTypes(IAssembliesResolver assembliesResolver)
        {
            var discovered = DefaultResolver.GetControllerTypes(assembliesResolver);
            return discovered
                .Where(t => t != null &&
                            !string.IsNullOrWhiteSpace(t.Namespace) &&
                            t.Namespace.StartsWith(AllowedNamespacePrefix, StringComparison.Ordinal))
                .ToList();
        }
    }
}
