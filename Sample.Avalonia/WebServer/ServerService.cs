using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Avalonia.WebServer {
    public class ServerApiStartup {
        readonly static string server = "http://localhost";
        readonly static string reactViewResources = "ReactViewResources";
        readonly static string customResourcePath = "custom/resource";

        public void ConfigureServices(IServiceCollection services) {
            services.AddResponseCompression();
        }
        public void Configure(IApplicationBuilder app) {
            //app.UseSession();
            //app.UseHttpsRedirection();
            app.UseResponseCompression();
            app.UseWebSockets(new WebSocketOptions() { KeepAliveInterval = TimeSpan.FromMinutes(15) });  // TODO TCS Review this timeout
            _ = app.Use(async (context, next) => {
                if (context.WebSockets.IsWebSocketRequest) {
                    using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var socketFinishedTcs = new TaskCompletionSource<object>();
                    AddSocket(webSocket, socketFinishedTcs);
                    await socketFinishedTcs.Task;
                } else {
                    // static resources
                    PathString path = context.Request.Path;
                    string prefix = $"/{reactViewResources}/{customResourcePath}";
                    if (path.StartsWithSegments(prefix)) {
                        var customPath = path.Value.Replace(prefix, "") + context.Request.QueryString;
                        string referer = context.Request.Headers["Referer"];
                        var nativeobjectname = referer.Split('&')[3];
                        using Stream stream = NativeObjects[nativeobjectname].GetCustomResource(customPath, out string extension);
                        context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
                        await stream.CopyToAsync(context.Response.Body);
                    } else {
                        if (path != "/favicon.ico") {
                            using Stream stream = ResourcesManager.TryGetResource(path, true, out string extension);
                            context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
                            await stream.CopyToAsync(context.Response.Body);
                        }
                    }
                }
            });
        }

        readonly static Dictionary<string, ExtendedReactViewFactory> NativeObjects = new Dictionary<string, ExtendedReactViewFactory>();

        internal static void NewNativeObject(string name, ExtendedReactViewFactory extendedReactViewFactory) {
            NativeObjects[name] = extendedReactViewFactory;
            // Just to test
            string url = $"{server}/{reactViewResources}/index.html?./&true&__Modules__&{name}&{customResourcePath}";
            if (NativeObjects.Count == 1) {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            } else {
                var text = $"{{ \"OpenURLInPopup\": \"{JsonEncodedText.Encode(url)}\", \"Arguments\":[] }}";
                var stream = Encoding.UTF8.GetBytes(text);
                _ = lastSocket.SendAsync(new ArraySegment<byte>(stream), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public static WebSocket NextWebSocket;

        private static WebSocket lastSocket;

        internal static void AddSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTcs) {
            NextWebSocket = socket;
            lastSocket = socket;
        }

    }

    internal class ServerService {
        public static void StartServer() {
            var serverService = App.ServiceProvider.GetService(typeof(ServerService)) as ServerService;
            serverService.RestartServer();
        }

        private IWebHost server = null;
        public void RestartServer() {
            StopServer();
            server = WebHost.CreateDefaultBuilder().UseKestrel(x => {
                var PortNumber = 80;
                x.ListenAnyIP(PortNumber);
                x.ListenLocalhost(PortNumber);
            }).UseStartup<ServerApiStartup>().UseDefaultServiceProvider((b, o) => {
            }).Build();

            // Starting;
            System.Threading.Tasks.Task.Run(() => {
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
