using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ReactViewWebServer {
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
