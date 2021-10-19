using System;
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

namespace ReactViewControl.WebServer {
    class ServerApiStartup {
        readonly static string ReactViewResources = "ReactViewResources";
        readonly static string CustomResourcePath = "custom/resource";

        public void ConfigureServices(IServiceCollection services) {
            //services.AddResponseCompression();
        }
        public void Configure(IApplicationBuilder app) {
            //app.UseSession();
            //app.UseHttpsRedirection();
            //app.UseResponseCompression();
            app.UseWebSockets(new WebSocketOptions() { KeepAliveInterval = TimeSpan.FromMinutes(15) });  // TODO TCS Review this timeout
            _ = app.Use(async (context, next) => {
                if (context.WebSockets.IsWebSocketRequest) {
                    using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync()) {
                        var socketFinishedTcs = new TaskCompletionSource<object>();
                        AddSocket(webSocket, socketFinishedTcs);
                        await socketFinishedTcs.Task;
                    }
                } else {
                    // static resources
                    PathString path = context.Request.Path;
                    string prefix = $"/custom/resource";
                    if (path != "/favicon.ico") {
                    }
                    if (path.Value.Contains(prefix)) {
                        var customPath = path.Value.Substring(path.Value.IndexOf(prefix)).Replace(prefix, "") + context.Request.QueryString;
                        string referer = context.Request.Headers["Referer"];
                        var nativeobjectname = Regex.Match(referer ?? "", "__NativeAPI__\\d*").Value;
                        var nativeObject = nativeobjectname != "" ? NativeObjects[nativeobjectname] : lastNativeObject;
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

        readonly static Dictionary<string, ServerView> NativeObjects = new Dictionary<string, ServerView>();


        static string StarterURL;

        static ServerView lastNativeObject;

        internal static void NewNativeObject(string name, ServerView serverView) {
            NativeObjects[name] = serverView;
            lastNativeObject = serverView;
            string url = $"/{ReactViewResources}/index.html?./&true&__Modules__&{name}&{CustomResourcePath}";
            if (NativeObjects.Count == 1) {
                StarterURL = url;
                Process.Start(new ProcessStartInfo("cmd", $"/c start http://localhost/") { CreateNoWindow = true });
            } else {
                _ = Task.Run(() => {
                    while (firstSocket == null) {
                        Task.Delay(100);
                    }
                    var text = $"{{ \"";//  OpenURL\": \"{JsonEncodedText.Encode(url)}\", \"Arguments\":[] }}";
                    switch (serverView.GetViewName()) {
                        case "ReactViewHostForPlugins":
                        case "DialogView":
                            text += "OpenURLInPopup";
                            break;
                        case "TooltipView":
                            //text += "OpenTooltip";
                            //break;
                            return;
                        case "WorkspaceView":
                            text += "OpenURL";
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    text += $"\": \"{JsonEncodedText.Encode(url)}\", \"Arguments\":[] }}";
                    var stream = Encoding.UTF8.GetBytes(text);
                    if (firstSocket.State == WebSocketState.Open) {
                        _ = firstSocket.SendAsync(new ArraySegment<byte>(stream), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                });
            }
        }

        public static WebSocket NextWebSocket;

        static WebSocket firstSocket = null;

        internal static void AddSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTcs) {
            NextWebSocket = socket;
            if (firstSocket == null) {
                firstSocket = socket;
                Task.Run(() => {
                    while (firstSocket.State != WebSocketState.Closed) {
                        Task.Delay(100);
                    }
                    Environment.Exit(0);
                });
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
            server = WebHost.CreateDefaultBuilder().UseUrls("http://*:80", "http://*:8080", "https://*:443").UseKestrel()
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
