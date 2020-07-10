using System;
using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using UIAutomation.Framework.Services;
using UIAutomation.Framework.Utils;

namespace UIAutomation.Framework
{
    public class AppSession
    {

        protected static WindowsDriver<WindowsElement> session;
        protected static WebService webServices;

        public static void Setup()
        {
            Logger.Instance.Info($"Starting Setup");
            Logger.Instance.Info("Starting Hub Service");
            HubService.GetInstance();

            Logger.Instance.Info("Reading Configuration");
            var config = ConfigurationService.GetConfiguration();
            if (session == null)
            {
                Logger.Instance.Info($"Starting {config.AppName} application");
                AppiumOptions startAppOptions = PrepareOptionsForApp(config.PathToApp);
                try
                {
                    new WindowsDriver<WindowsElement>(new Uri(config.WindowsApplicationDriverUrl), startAppOptions);
                } catch (Exception)
                {
                    // catch any exception after startup
                }

                Logger.Instance.Info($"Looking for desktop root");
                AppiumOptions desktopOptions = PrepareOptionsForApp("Root");
                session = new WindowsDriver<WindowsElement>(new Uri(config.WindowsApplicationDriverUrl), desktopOptions);
                session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(config.Timeout);
                Assert.IsNotNull(session);

                Logger.Instance.Info($"Looking for {config.AppName} application Window");
                var ApplicationWindow = session.FindElementByName(config.AppName);
                ApplicationWindow.Click();
                var ApplicationSessionHandle = ApplicationWindow.GetAttribute("NativeWindowHandle");
                ApplicationSessionHandle = (int.Parse(ApplicationSessionHandle)).ToString("x");

                Logger.Instance.Info($"Attaching to {config.AppName} application Window");
                AppiumOptions appOptions = new AppiumOptions();
                appOptions.AddAdditionalCapability("appTopLevelWindow", ApplicationSessionHandle);
                session = new WindowsDriver<WindowsElement>(new Uri(config.WindowsApplicationDriverUrl), appOptions);

                session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(config.Timeout);

                Logger.Instance.Info($"Attaching webdriver to {config.AppName} application using {config.DebugPort} port");
                webServices = WebService.Init(config.PathToApp, config.DebugPort, config.ChromiumVersion);

                Logger.Instance.Info($"Setup Finished");
            }
        }

        public static void TearDown()
        {

            Logger.Instance.Info($"Starting TearDown");
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
            Logger.Instance.Info($"TearDown Finished");
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
