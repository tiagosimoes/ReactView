using OpenQA.Selenium;
using UIAutomation.Framework.Services;
using UIAutomation.Framework.Utils;

namespace UIAutomation.Framework.WebElements
{
    public class Element
    {
        private By locator;
        private string name;
        private string[] shadowRootSelectors;
        private IJavaScriptExecutor jsExecutor => WebService.GetInstance().Browser;

        public Element(By locator, string name, string[] shadowRootSelectors = null)
        {
            this.locator = locator;
            this.name = name;
            this.shadowRootSelectors = shadowRootSelectors;
        }

        public string Text => GetElement().Text;

        public void Click(bool force = false)
        {
            Logger.Instance.Info($"Clicking {name} web element");
            if (force)
            {
                jsExecutor.ExecuteScript("arguments[0].click();", GetElement());
            } else
            {
                GetElement().Click(); 
            }
        }

        public void ClearAndType(string text)
        {
            Logger.Instance.Info($"ClearAndType action for {name} web element, typing {text}");
            GetElement().SendKeys(text);
            GetElement().Clear();
            GetElement().SendKeys(text);
        }

        private IWebElement GetElement()
        {
            if(shadowRootSelectors == null)
            {
                return WebService.GetInstance().Browser.FindElement(locator);
            }

            return GetShadowRootElement(shadowRootSelectors).FindElement(locator);
        }

        private IWebElement GetShadowRootElement(params string[] locators)
        {
            IWebElement root = (IWebElement) jsExecutor.ExecuteScript("return document");

            foreach (var locator in locators)
            {
                root = (IWebElement) jsExecutor.ExecuteScript("return arguments[0].querySelector(arguments[1]).shadowRoot", root, locator);
            }

            return root;
        }
    }
}
