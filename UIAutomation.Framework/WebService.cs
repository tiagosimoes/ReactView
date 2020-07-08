using Castle.Core.Internal;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace UIAutomation.Framework
{
    public class WebService
    {
        private static WebService instance;
        private readonly List<string> registeredHandles = new List<string>();
        public ChromeDriver Browser;

        public static WebService GetInstance()
        {
            if (instance == null)
            {
                throw new Exception("The WebService was not initialized. Use Init method to Initialize WebService;");
            }

            return instance;
        }

        public static WebService Init(string pathToApp, int debugPort, string chromeVersion)
        {
            instance = new WebService(pathToApp, debugPort, chromeVersion);
            return instance;
        }

        private WebService(string pathToApp, int debugPort, string chromeVersion)
        {
            new DriverManager().SetUpDriver(new ChromeConfig(), chromeVersion);

            ChromeOptions options = new ChromeOptions
            {
                BinaryLocation = pathToApp,
                DebuggerAddress = $"localhost:{debugPort}"
            };
            options.AddArgument($"remote-debugging-port={debugPort}");

            Browser = new ChromeDriver(options);
            UpdateHandles();
            SwitchToTab(0);
        }

        public void SwitchToTab(int tabIndex)
        {
            var handlesCount = registeredHandles.Count;

            if (handlesCount - 1 < tabIndex)
            {
                throw new Exception($"Only {handlesCount - 2} tabs are opened. Please make sure your tab was opened!");
            }

            Browser.SwitchTo().Window(registeredHandles[tabIndex]);
        }

        internal void Quit()
        {
            if (Browser != null)
            {
                Browser.Quit();
            }

            instance = null;
        }

        public void UpdateHandles()
        {
            var newHandles = Browser.WindowHandles.ToList();
            if (registeredHandles.IsNullOrEmpty())
            {
                registeredHandles.Add(newHandles[1]);
                registeredHandles.Add(newHandles[0]);
            }
            else
            {
                foreach (var handle in newHandles)
                {
                    if (!registeredHandles.Contains(handle))
                    {
                        registeredHandles.Add(handle);
                    }
                }
            }
        }
    }
}