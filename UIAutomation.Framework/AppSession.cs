using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using System;

namespace UIAutomation.Framework
{
    public class AppSession
    {
        private const string appName = "Example";
        private const int timeout = 20;
        private const string windowsApplicationDriverUrl = "http://127.0.0.1:4723";
        private const string pathToApp = @"C:\Users\Xiaomi\Documents\OutSystems\WebView\Example.Avalonia\bin\Release\netcoreapp3.1\Example.Avalonia.exe";
        private const int debugPort = 9090;
        private const string chromiumVersion = "75.0.3770.90";

        protected static WindowsDriver<WindowsElement> session;
        protected static WebService webServices;

        public static void Setup()
        {
            if (session == null)
            {
                AppiumOptions startAppOptions = PrepareOptionsForApp(pathToApp);
                try
                {
                    new WindowsDriver<WindowsElement>(new Uri(windowsApplicationDriverUrl), startAppOptions);
                } catch (Exception)
                {
                    // catch any exception after startup
                }

                AppiumOptions desktopOptions = PrepareOptionsForApp("Root");
                session = new WindowsDriver<WindowsElement>(new Uri(windowsApplicationDriverUrl), desktopOptions);
                session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeout);
                Assert.IsNotNull(session);

                var ApplicationWindow = session.FindElementByName(appName);
                ApplicationWindow.Click();
                var ApplicationSessionHandle = ApplicationWindow.GetAttribute("NativeWindowHandle");
                ApplicationSessionHandle = (int.Parse(ApplicationSessionHandle)).ToString("x");

                AppiumOptions appOptions = new AppiumOptions();
                appOptions.AddAdditionalCapability("appTopLevelWindow", ApplicationSessionHandle);
                session = new WindowsDriver<WindowsElement>(new Uri(windowsApplicationDriverUrl), appOptions);

                session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeout);

                webServices = WebService.Init(pathToApp, debugPort, chromiumVersion);
            }
        }

        public static void TearDown()
        {
            if (session != null)
            {
                session.CloseApp();
                session.Quit();
                session = null;
            }

            if(webServices != null)
            {
                webServices.Quit();
            }
        }

        private static AppiumOptions PrepareOptionsForApp(string pathToApp)
        {
            AppiumOptions options = new AppiumOptions();
            options.AddAdditionalCapability("platformName", "Windows");
            options.AddAdditionalCapability("app", pathToApp);
            options.AddAdditionalCapability("deviceName", "WindowsPC");
            return options;
        }
    }
}
