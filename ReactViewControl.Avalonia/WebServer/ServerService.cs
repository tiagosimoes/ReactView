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
            app.Use(async (context, next) =>
            {
                if(context.Request.Host.Value != "localhost"){
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
            app.UseWebSockets(new WebSocketOptions() { KeepAliveInterval = TimeSpan.FromMinutes(15) });  // TODO TCS Review this timeout
            _ = app.Use(async (context, next) => {
                if (context.WebSockets.IsWebSocketRequest) {
                    using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync()) {
                        var socketFinishedTcs = new TaskCompletionSource<object>();
                        AddSocket(webSocket, socketFinishedTcs, context.Request.Path);
                        await socketFinishedTcs.Task;
                    }
                } else {
                    if (context.Request.Host.Value != "localhost") {
                        var responseCachingFeature = context.Features.Get<IResponseCachingFeature>();
                        if (responseCachingFeature != null) {
                            responseCachingFeature.VaryByQueryKeys = new[] { "*" };
                        }
                    }
                    // static resources
                    PathString path = context.Request.Path;
                    string prefix = $"/custom/resource";
                    if (path != "/favicon.ico") {
                    }
                    if (path.Value.Contains(prefix)) {
                        var customPath = path.Value.Substring(path.Value.IndexOf(prefix)).Replace(prefix, "") + context.Request.QueryString;
                        string referer = context.Request.Headers["Referer"];
                        var nativeobjectname = Regex.Match(referer ?? "", "__NativeAPI__\\d*").Value;
                        var nativeObject = nativeobjectname != "" ? connections.FirstOrDefault(conn => conn.NativeObjectName == nativeobjectname).serverView : connections.Last().serverView;
                        Stream stream = nativeObject.GetCustomResource(customPath, out string extension);
                        context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
                        await stream.CopyToAsync(context.Response.Body);

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



        static string StarterURL;

        internal static void NewNativeObject(string name, ServerView serverView) {
            connections.Add(new Connection(name, serverView));
            string url = $"/{ReactViewResources}/index.html?./&true&__Modules__&{name}&{CustomResourcePath}";
            if (connections.Count == 1) {
                StarterURL = url;
                Process.Start(new ProcessStartInfo("cmd", $"/c start http://localhost/") { CreateNoWindow = true });
            } else {
                _ = Task.Run(() => {
                    while (LastWebSocketWithActivity == null) {
                        Task.Delay(100);
                    }
                    var text = $"{{ \"";
                    switch (serverView.GetViewName()) {
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
                    var stream = Encoding.UTF8.GetBytes(text);
                    if (LastWebSocketWithActivity.State == WebSocketState.Open) {
                        _ = LastWebSocketWithActivity.SendAsync(new ArraySegment<byte>(stream), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                });
            }
        }

        class Connection {
            public string NativeObjectName;
            public ServerView serverView;
            public WebSocket socket;
            public bool isWorkspace;
            public Connection(string nativeObjectName, ServerView serverView) {
                this.NativeObjectName = nativeObjectName;
                this.serverView = serverView;
                this.socket = null;
                this.isWorkspace = false;
            }
        }

        static List<Connection> connections = new List<Connection>();

        public static WebSocket NextWebSocket;
        public static WebSocket LastWebSocketWithActivity;


        internal static void AddSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTcs, string path) {
            var connection = connections.Find(conn => conn.NativeObjectName == path.Substring(1));
            connections.Find(conn => conn == connection).socket = socket;
            NextWebSocket = socket;
            connection.serverView.SetPopupDimensionsIfNeeded();
        }


        internal static void CloseSocket(WebSocket webSocket) {
            connections.Remove(connections.Find(con => con.socket == webSocket));
            if (connections.Count == 0) {
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
                serverOptions.ConfigureHttpsDefaults(listenOptions => {
                    if (File.Exists("outsystemsstudio.pfx")) { 
                        listenOptions.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2("outsystemsstudio.pfx");
                    }
                });
            }).UseStartup<ServerApiStartup>().UseDefaultServiceProvider((b, o) => {
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
