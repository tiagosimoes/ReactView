using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.Extensions.DependencyInjection;
using ReactViewControl.WebServer;
using ReactViewWebServer;

namespace ReactViewWebServer {
    class ServerConfigStartup {

        public void ConfigureServices(IServiceCollection services) {
            services.AddResponseCompression();
            services.AddResponseCaching();
        }
        public void Configure(IApplicationBuilder app) {
            //app.UseSession();
            //app.UseHttpsRedirection();
            app.UseResponseCaching();
            app.UseResponseCompression();
            ConfigureCaching(app);
            app.UseWebSockets(new WebSocketOptions() { KeepAliveInterval = TimeSpan.FromMinutes(15) });  // TODO TCS Review this timeout
            _ = app.Use(async (context, next) => {
                if (context.WebSockets.IsWebSocketRequest) {
                    using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync()) {
                        var socketFinishedTcs = new TaskCompletionSource<object>();
                        ServerAPI.AddSocket(webSocket, socketFinishedTcs, context.Request.Path);
                        await socketFinishedTcs.Task;
                    }
                } else {
                    ConfigureCachingHeaders(context);
                    // static resources
                    PathString path = context.Request.Path;
                    string prefix = $"/custom/resource";
                    if (path.Value.Contains(prefix)) {
                        var customPath = path.Value.Substring(path.Value.IndexOf(prefix)).Replace(prefix, "") + context.Request.QueryString;
                        string referer = context.Request.Headers["Referer"];
                        var nativeobjectname = Regex.Match(referer ?? "", "__NativeAPI__\\d*").Value;
                        Stream stream = ServerAPI.GetCustomResource(nativeobjectname, customPath, out string extension);
                        context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
                        await stream.CopyToAsync(context.Response.Body);
                        stream.Position = 0;
                    } else if (path.Value == "/") {
                        while (ServerAPI.StarterURL == null) {
                        }
                        context.Response.Redirect(ServerAPI.StarterURL);
                    } else {
                        if (path == "/favicon.ico") {
                            path = "/ServiceStudio.Common/Images/OutSystems.ico";
                        }
                        using (Stream stream = ResourcesManager.TryGetResource(path, true, out string extension)) {
                            context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
                            await stream.CopyToAsync(context.Response.Body);
                        }
                    }
                }
            });
        }

        internal static void OpenURL(Uri uri) {
            ServerAPI.OpenURL(uri);
        }

        private static void ConfigureCachingHeaders(HttpContext context) {
            if (context.Request.Host.Value != "localhost") {
                var responseCachingFeature = context.Features.Get<IResponseCachingFeature>();
                if (responseCachingFeature != null) {
                    responseCachingFeature.VaryByQueryKeys = new[] { "*" };
                }
            }
        }

        private static void ConfigureCaching(IApplicationBuilder app) {
            app.Use(async (context, next) => {
                if (context.Request.Host.Value != "localhost") {
                    context.Response.GetTypedHeaders().CacheControl =
                        new Microsoft.Net.Http.Headers.CacheControlHeaderValue() {
                            Public = true,
                            MaxAge = TimeSpan.FromSeconds(120)
                        };
                    context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
                        new string[] { "Accept-Encoding" };
                }
                await next();
            });
        }

    }


}
