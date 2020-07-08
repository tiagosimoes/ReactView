using OpenQA.Selenium;

namespace UIAutomation.Framework.WebElements
{
    public class Button : Element
    {
        public Button(By locator, string name, string[] shadowRootSelectors = null) : base(locator, name, shadowRootSelectors)
        {
        }
    }
}
