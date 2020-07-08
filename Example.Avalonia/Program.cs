using Avalonia;
using System;

namespace Example.Avalonia {
    class Program {
        static void Main(string[] args) {
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .StartWithClassicDesktopLifetime(new string[1] { "--remote-debugging-port=9090" });
        }
    }
}
