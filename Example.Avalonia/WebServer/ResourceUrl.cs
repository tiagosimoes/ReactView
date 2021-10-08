using System;
using System.Linq;
using System.Reflection;

namespace Example.Avalonia.WebServer {

    public partial class ResourceUrl {

        public const string LocalScheme = "local";
        public const string CustomScheme = "custom";

        internal const string EmbeddedScheme = "embedded";
        internal const string PathSeparator = "/";

        private const string AssemblyPathSeparator = ";";
        private const string AssemblyPrefix = "assembly:";
        

        private static bool ContainsAssemblyLocation(string url) {
            return url.StartsWith(EmbeddedScheme + PathSeparator + AssemblyPrefix);
        }

        internal static string[] GetEmbeddedResourcePath(string resourceUrl) {
            if (ContainsAssemblyLocation(resourceUrl)) {
                var indexOfPath = resourceUrl.IndexOf(AssemblyPathSeparator);
                return resourceUrl[(indexOfPath
                    + 1)..].Split(new[] { PathSeparator }, StringSplitOptions.None);
            }
            var uriParts = resourceUrl.Split("/");
            return uriParts.Skip(1).Select(p => p.Replace(PathSeparator, "")).ToArray();
        }

        public static string GetEmbeddedResourceAssemblyName(string resourceUrl) {
            if (ContainsAssemblyLocation(resourceUrl)) {
                var resourcePath = resourceUrl.Substring((PathSeparator + AssemblyPrefix).Length);
                var indexOfPath = Math.Max(0, resourcePath.IndexOf(AssemblyPathSeparator));
                return resourcePath.Substring(0, indexOfPath);
            }
            var segments = resourceUrl.Split("/");
            if (segments.Length > 1) {
                var assemblySegment = segments[1];
                return assemblySegment.EndsWith(PathSeparator) ? assemblySegment.Substring(0, assemblySegment.Length - PathSeparator.Length) : assemblySegment; // default assembly name to the first path
            }
            return string.Empty;
        }

    }
}
