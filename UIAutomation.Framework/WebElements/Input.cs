using OpenQA.Selenium;

namespace UIAutomation.Framework.WebElements
{
    public class Input : Element
    {
        public Input(By locator, string name, string[] shadowRootSelectors = null) : base(locator, name, shadowRootSelectors)
        {
        }
    }
}
