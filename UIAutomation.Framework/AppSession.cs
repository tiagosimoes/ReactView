using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using System;

namespace UIAutomation.Framework
{
    public class AppSession
    {
        private const string AppName = "Example";
        private const int timeout = 20;
        private const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        private const string PathToApp = @"C:\Users\Xiaomi\Documents\OutSystems\WebView\Example.Avalonia\bin\Release\netcoreapp3.1\Example.Avalonia.exe";

        protected static WindowsDriver<WindowsElement> session;

        public static void Setup()
        {
            if (session == null)
            {
                AppiumOptions startAppOptions = PrepareOptionsForApp(PathToApp);
                try
                {
                    new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), startAppOptions);
                } catch (Exception)
                {
                    // catch any exception after startup
                }

                AppiumOptions desktopOptions = PrepareOptionsForApp("Root");
                session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), desktopOptions);
                session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeout);
                Assert.IsNotNull(session);

                var ApplicationWindow = session.FindElementByName(AppName);
                ApplicationWindow.Click();
                var ApplicationSessionHandle = ApplicationWindow.GetAttribute("NativeWindowHandle");
                ApplicationSessionHandle = (int.Parse(ApplicationSessionHandle)).ToString("x");

                AppiumOptions appOptions = new AppiumOptions();
                appOptions.AddAdditionalCapability("appTopLevelWindow", ApplicationSessionHandle);
                session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), appOptions);

                session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeout);
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
