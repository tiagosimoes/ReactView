using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Example.Avalonia.WebServer {
    public class ServerApiStartup {

        public void Configure(IApplicationBuilder app) {
            //app.UseSession();
            //app.UseHttpsRedirection();
            string server = "http://localhost:8080";
            string reactViewResources = "ReactViewResources";
            string customResourcePath = "custom/resource";
            app.UseWebSockets(new WebSocketOptions() { KeepAliveInterval = TimeSpan.FromMinutes(15) });  // TODO TCS Review this timeout
            _ = app.Use(async (context, next) => {
                if (context.WebSockets.IsWebSocketRequest) {
                    using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var socketFinishedTcs = new TaskCompletionSource<object>();
                    BackgroundSocketProcessor.AddSocket(webSocket, socketFinishedTcs);
                    await socketFinishedTcs.Task;
                } else {
                    // static resources
                    PathString path = context.Request.Path;
                    if (path.ToString().StartsWith($"/{reactViewResources}/{customResourcePath}/")) {
                        // TODO TCS Handle custom resources (per view)
                    } else {
                        using Stream stream = ResourcesManager.TryGetResource(path, true, out string extension);
                        context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
                        await stream.CopyToAsync(context.Response.Body);
                    }
                }
            });
            // Just to test
            string url = $"{server}/{reactViewResources}/index.html?./&true&__Modules__&__NativeAPI__&{customResourcePath}";
            url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }

        public static async Task SendWebSocketMessage(string message) {
            var stream = Encoding.UTF8.GetBytes(message);
            while (WebSocket == null) {
                await Task.Delay(25);
            }
            await WebSocket.SendAsync(new ArraySegment<byte>(stream), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static WebSocket WebSocket;
        internal class BackgroundSocketProcessor {
            internal static void AddSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTcs) {
                WebSocket = socket;
                _ = OnWebSocketMessageReceived(socket);
            }
        }
        public static Action<string> ProcessMessage; 
        private static async Task OnWebSocketMessageReceived(WebSocket webSocket) {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue) {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ProcessMessage(text);
                //await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
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
                var PortNumber = 8080;
                x.ListenAnyIP(PortNumber);
                x.ListenLocalhost(PortNumber);
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
