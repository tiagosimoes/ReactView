using OpenQA.Selenium;
using UIAutomation.Framework.NativeElements;
using UIAutomation.Framework.Services;
using UIAutomation.Framework.Utils;
using UIAutomation.Framework.WebElements;

namespace UIAutomation.Tests.Example.PageObjects
{
    public class TabViewPage
    {
        private readonly NativeElement actionsNative = new NativeElement(ResourceUtil.PatternPath("actions.png"), "Actions");
        private readonly NativeElement newTabOption = new NativeElement(ResourceUtil.PatternPath("newTab.png"), "Actions -> New Tab");
        private readonly Input notShadowRootInput = new Input(By.XPath("//*[@id='webview_root']/div/input"), "Input");
        private readonly Button shadowRootButton = new Button(By.CssSelector("#webview_root > div > button"), "Button", new string[] { "#webview_root > div > div:nth-child(10) > div" });
        private readonly Label shadowRootText = new Label(By.CssSelector("#webview_root > div"), "Clicks counter", new string[] { "#webview_root > div > div:nth-child(10) > div" });
        private NativeElement ViewTab(int index) => new NativeElement(ResourceUtil.PatternPath($"view{index}Tab.png"), $"View {index}");
        private NativeElement ViewTabSelected(int index) => new NativeElement(ResourceUtil.PatternPath($"view{index}TabSelected.png"), $"Selected View {index}");

        public void TypeIntoNotShadowRootInput(string text)
        {
            notShadowRootInput.ClearAndType(text);
        }

        public void ClickShadowRootButton()
        {
            shadowRootButton.Click(true);
        }

        public string GetTextFromShadowRootText()
        {
            return shadowRootText.Text;
        }

        public void AddNewTabFromActionsMenu()
        {
            actionsNative.Click();
            newTabOption.Click();
            WebService.GetInstance().UpdateHandles();
        }

        public void SelectTab(int index)
        {
            ViewTab(index).Click();
            WebService.GetInstance().SwitchToTab(index);
        }

        public bool IsTabSelected(int index)
        {
            return ViewTabSelected(index).IsExist(true);
        }
    }
}
