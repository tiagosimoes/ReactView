using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Example.Avalonia.WebServer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Example.Avalonia {
    public class App : Application {
        private static ServiceProvider serviceProvider;
        public static ServiceCollection Services { get; set; } = new ServiceCollection();
        public static ServiceProvider ServiceProvider { get => serviceProvider; set => serviceProvider = value; }

        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);
 
            Services.AddSingleton<ServerService>();
            Services.AddScoped<IHttpContextAccessor, HttpContextAccessor>();
            ServiceProvider = Services.BuildServiceProvider();
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.MainWindow = new MainWindow();
                ServerService.StartServer();
            }
            base.OnFrameworkInitializationCompleted();
        }



    }
}
