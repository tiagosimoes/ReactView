using System;
using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using UIAutomation.Framework.Services;

namespace UIAutomation.Framework
{
    public class AppSession
    {

        protected static WindowsDriver<WindowsElement> session;
        protected static WebService webServices;

        public static void Setup()
        {
            HubService.GetInstance();
            var config = ConfigurationService.GetConfiguration();
            if (session == null)
            {
                AppiumOptions startAppOptions = PrepareOptionsForApp(config.PathToApp);
                try
                {
                    new WindowsDriver<WindowsElement>(new Uri(config.WindowsApplicationDriverUrl), startAppOptions);
                } catch (Exception)
                {
                    // catch any exception after startup
                }

                AppiumOptions desktopOptions = PrepareOptionsForApp("Root");
                session = new WindowsDriver<WindowsElement>(new Uri(config.WindowsApplicationDriverUrl), desktopOptions);
                session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(config.Timeout);
                Assert.IsNotNull(session);

                var ApplicationWindow = session.FindElementByName(config.AppName);
                ApplicationWindow.Click();
                var ApplicationSessionHandle = ApplicationWindow.GetAttribute("NativeWindowHandle");
                ApplicationSessionHandle = (int.Parse(ApplicationSessionHandle)).ToString("x");

                AppiumOptions appOptions = new AppiumOptions();
                appOptions.AddAdditionalCapability("appTopLevelWindow", ApplicationSessionHandle);
                session = new WindowsDriver<WindowsElement>(new Uri(config.WindowsApplicationDriverUrl), appOptions);

                session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(config.Timeout);

                webServices = WebService.Init(config.PathToApp, config.DebugPort, config.ChromiumVersion);
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

            HubService.GetInstance().Dispose();
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
