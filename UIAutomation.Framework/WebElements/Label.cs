using OpenQA.Selenium;

namespace UIAutomation.Framework.WebElements
{
    public class Label : Element
    {
        public Label(By locator, string name, string[] shadowRootSelectors = null) : base(locator, name, shadowRootSelectors)
        {
        }
    }
}
