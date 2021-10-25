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

namespace ReactViewControl.WebServer {
    class ServerApiStartup {
        readonly static string ReactViewResources = "ReactViewResources";
        readonly static string CustomResourcePath = "custom/resource";

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
                        AddSocket(webSocket, socketFinishedTcs, context.Request.Path);
                        await socketFinishedTcs.Task;
                    }
                } else {
                    ConfigureCachingHeaders(context);
                    // static resources
                    PathString path = context.Request.Path;
                    string prefix = $"/custom/resource";
                    if (path.Value.Contains(prefix)) {
                        var customPath = path.Value.Substring(path.Value.IndexOf(prefix)).Replace(prefix, "") + context.Request.QueryString;
                        await GetCustomResource(context, customPath);
                    } else if (path.Value == "/") {
                        while (StarterURL == null) {
                        }
                        context.Response.Redirect(StarterURL);
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

        private static async Task GetCustomResource(HttpContext context, string customPath) {
            string referer = context.Request.Headers["Referer"];
            var nativeobjectname = Regex.Match(referer ?? "", "__NativeAPI__\\d*").Value;
            var nativeObject = nativeobjectname != "" ? ServerViews.FirstOrDefault(conn => conn.NativeAPIName == nativeobjectname) : ServerViews.Last();
            Stream stream = nativeObject.GetCustomResource(customPath, out string extension);
            context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
            await stream.CopyToAsync(context.Response.Body);
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

        static string StarterURL;

        internal static void NewNativeObject(ServerView serverView) {
            ServerViews.Add(serverView);
            string url = $"/{ReactViewResources}/index.html?./&true&__Modules__&{serverView.NativeAPIName}&{CustomResourcePath}";
            if (ServerViews.Count == 1) {
                StarterURL = url;
                Process.Start(new ProcessStartInfo("cmd", $"/c start http://localhost/") { CreateNoWindow = true });
            } else {
                _ = Task.Run(() => {
                    while (lastServerViewWithActivity == null) {
                        Task.Delay(1);
                    }
                    var text = $"{{ \"";
                    switch (serverView.GetViewName()) {
                        case "AIContextSuggestionsMenuView":
                        case "ReactViewHostForPlugins":
                        case "DialogView":
                            text += "OpenURLInPopup";
                            break;
                        case "TooltipView":
                            // TODO TCS, fix tooltips 
                            //text += "OpenTooltip";
                            //break;
                            return;
                        case "WorkspaceView":
                        default:
                            text += "OpenURL";
                            break;
                    }
                    text += $"\": \"{JsonEncodedText.Encode(url)}\", \"Arguments\":[] }}";
                    _ = lastServerViewWithActivity.SendWebSocketMessage(text);
                });
            }
        }

        internal static void SetLastConnectionWithActivity(ServerView serverView) {
            lastServerViewWithActivity = serverView;
        }

        static readonly List<ServerView> ServerViews = new List<ServerView>();
        private static ServerView lastServerViewWithActivity;

        internal static void AddSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTcs, string path) {
            var serverView = ServerViews.Find(conn => conn.NativeAPIName == path.Substring(1));
            serverView.SetSocket(socket);
        }

        internal static void CloseSocket(ServerView serverView) {
            ServerViews.Remove(serverView);
            if (ServerViews.Count == 0) {
                Environment.Exit(0);
            }
        }
    }

    internal class ServerService {
        private static ServiceProvider serviceProvider;
        public static ServiceCollection Services { get; set; } = new ServiceCollection();
        public static ServiceProvider ServiceProvider { get => serviceProvider; set => serviceProvider = value; }

        public static void StartServer() {

            Services.AddSingleton<ServerService>();
            Services.AddScoped<IHttpContextAccessor, HttpContextAccessor>();
            ServiceProvider = Services.BuildServiceProvider();

            var serverService = ServiceProvider.GetService(typeof(ServerService)) as ServerService;
            serverService.RestartServer();
        }

        private IWebHost server = null;
        public void RestartServer() {
            StopServer();
            server = WebHost.CreateDefaultBuilder().UseUrls("http://*:80", "https://*:443", "http://*:8080").UseKestrel()
                .ConfigureKestrel(serverOptions => {
                    if (File.Exists("outsystemsstudio.pfx")) {
                        serverOptions.ConfigureHttpsDefaults(listenOptions => {
                            listenOptions.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2("outsystemsstudio.pfx");
                        });
                    }
                })
            .UseStartup<ServerApiStartup>().UseDefaultServiceProvider((b, o) => {
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
