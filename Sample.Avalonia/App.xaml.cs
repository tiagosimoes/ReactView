using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Sample.Avalonia.WebServer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebViewControl;

namespace Sample.Avalonia {

    public class App : Application {
        public static ServiceProvider ServiceProvider;
        public static ServiceCollection Services { get; set; } = new ServiceCollection();
        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);
 
            Services.AddSingleton<ServerService>();
            Services.AddScoped<IHttpContextAccessor, HttpContextAccessor>();
            ServiceProvider = Services.BuildServiceProvider();
            WebView.Settings.OsrEnabled = false;
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                ServerService.StartServer();
                Thread.Sleep(300); // TODO TCS Review this timeout
                desktop.MainWindow = new MainWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }



    }
}
