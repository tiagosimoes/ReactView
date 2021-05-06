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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Avalonia.WebServer {
    public class ServerApiStartup {

        public void Configure(IApplicationBuilder app) {
            //app.UseSession();
            //app.UseHttpsRedirection();
            app.UseWebSockets(new WebSocketOptions() { KeepAliveInterval = TimeSpan.FromMinutes(15) });
            app.Use(async (context, next) => {
                if (context.WebSockets.IsWebSocketRequest) {
                    using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync()) {
                        var socketFinishedTcs = new TaskCompletionSource<object>();
                        BackgroundSocketProcessor.AddSocket(webSocket, socketFinishedTcs);
                        await socketFinishedTcs.Task;
                    }
                } else {
                    // static resources
                    var path = context.Request.Path;
                    var stream = ResourcesManager.TryGetResource(path, true, out string extension);
                    context.Response.ContentType = ResourcesManager.GetExtensionMimeType(extension);
                    await stream.CopyToAsync(context.Response.Body);
                }
            });
            // Just to test
            var url = "http://localhost:8080/ReactViewResources/index.html?./&true&__Modules__&__NativeAPI__&custom/resource";
            url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }

        public static async System.Threading.Tasks.Task SendWebSocketMessage(string message) {
            var stream = Encoding.UTF8.GetBytes(message);
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
        private static async System.Threading.Tasks.Task OnWebSocketMessageReceived(WebSocket webSocket) {
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
            this.server = WebHost.CreateDefaultBuilder().UseKestrel(x => {
                var PortNumber = 8080;
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
            if (this.server != null) {
                // Shutting down
                this.server.StopAsync().Wait();
            }
            // Down
        }
    }
}
