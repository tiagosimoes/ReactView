using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.ResponseCaching;
using ReactViewControl.WebServer;

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
                        using (Stream stream = ServerAPI.GetCustomResource(nativeobjectname, customPath, out string extension)) {
                            context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
                            await stream.CopyToAsync(context.Response.Body);
                        }
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

    public class ServerService {
        private static ServiceProvider serviceProvider;
        private static ServiceCollection Services { get; set; } = new ServiceCollection();
        private static ServiceProvider ServiceProvider { get => serviceProvider; set => serviceProvider = value; }

        public static void StartServer() {

            Services.AddSingleton<ServerService>();
            Services.AddScoped<IHttpContextAccessor, HttpContextAccessor>();
            ServiceProvider = Services.BuildServiceProvider();

            var serverService = ServiceProvider.GetService(typeof(ServerService)) as ServerService;
            serverService.RestartServer();
        }

        private IWebHost server = null;
        private void RestartServer() {
            StopServer();
            server = WebHost.CreateDefaultBuilder().UseUrls("http://*:80", "https://*:443", "http://*:8080").UseKestrel()
                .ConfigureKestrel(serverOptions => {
                    if (File.Exists("outsystemsstudio.pfx")) {
                        serverOptions.ConfigureHttpsDefaults(listenOptions => {
                            listenOptions.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2("outsystemsstudio.pfx");
                        });
                    }
                })
            .UseStartup<ServerConfigStartup>().UseDefaultServiceProvider((b, o) => {
            }).Build();

            // Starting;
            Task.Run(() => {
                server.RunAsync();
                // Started;
            });
        }

        public void StopServer() {
            if (server != null) {
                // Shutting down
                server.StopAsync().Wait();
            }
            // Down
        }
    }
}
