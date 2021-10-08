using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.StaticFiles;

namespace Example.Avalonia.WebServer {

    public static partial class ResourcesManager {

        private static readonly AssemblyCache Cache = new AssemblyCache();

        private static string ComputeEmbeddedResourceName(string defaultNamespace, IEnumerable<string> resourcePath) {
            var resourceParts = (new[] { defaultNamespace }).Concat(resourcePath).ToArray();
            for (int i = 0; i < resourceParts.Length - 1; i++) {
                resourceParts[i] = resourceParts[i].Replace('-', '_').Replace('@', '_');
            }
            return string.Join(".", resourceParts);
        }

        private static Stream InternalTryGetResource(Assembly assembly, string defaultNamespace, IEnumerable<string> resourcePath, bool failOnMissingResource) {
            var resourceName = ComputeEmbeddedResourceName(defaultNamespace, resourcePath);
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (failOnMissingResource && stream == null) {
                throw new InvalidOperationException("Resource not found: " + resourceName);
            }
            return stream;
        }

        public static Stream TryGetResourceWithFullPath(Assembly assembly, IEnumerable<string> resourcePath) {
            return InternalTryGetResource(assembly, resourcePath.First(), resourcePath.Skip(1), false);
        }

        internal static Stream TryGetResource(string url, bool failOnMissingAssembly, out string extension) {
            var resourceAssembly = Cache.ResolveResourceAssembly(url, failOnMissingAssembly);
            if (resourceAssembly == null) {
                extension = string.Empty;
                return null;
            }
            var resourcePath = ResourceUrl.GetEmbeddedResourcePath(url);

            extension = Path.GetExtension(resourcePath.Last()).ToLower();
            var resourceStream = TryGetResourceWithFullPath(resourceAssembly, resourcePath);

            return resourceStream;
        }

        public static string GetExtensionMimeType(string extension) {
            extension = string.IsNullOrEmpty(extension) ? ".html" : extension;
            var mimeTypeProvider = new FileExtensionContentTypeProvider();
            mimeTypeProvider.Mappings.TryGetValue(extension, out var mimetype);
            return mimetype ?? "application/octet-stream";
        }

    }
}
