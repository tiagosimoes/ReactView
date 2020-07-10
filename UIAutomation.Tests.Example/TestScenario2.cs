using NUnit.Framework;
using UIAutomation.Tests.Example;
using UIAutomation.Tests.Example.PageObjects;

namespace UIAutomation.Framework { 

[TestFixture]
public class TestScenario2 : BaseTest
{
        [Test]
        public void ClickSomething2()
        {
            var tabViewPage = new TabViewPage();
            tabViewPage.TypeIntoNotShadowRootInput("Test text");
            tabViewPage.ClickShadowRootButton();
            tabViewPage.TypeIntoNotShadowRootInput("New Test text");
            tabViewPage.ClickShadowRootButton();

            Assert.IsTrue(tabViewPage.GetTextFromShadowRootText().Contains("Button clicks count: 2"), $"View text does not contain Expected value.");

            tabViewPage.AddNewTabFromActionsMenu();
            tabViewPage.SelectTab(1);

            Assert.IsTrue(tabViewPage.IsTabSelected(1), "Tab was not selected!");

            tabViewPage.TypeIntoNotShadowRootInput("Text for New Tab");
            tabViewPage.ClickShadowRootButton();
            tabViewPage.ClickShadowRootButton();
            tabViewPage.ClickShadowRootButton();

            Assert.IsTrue(tabViewPage.GetTextFromShadowRootText().Contains("Button clicks count: 3"), $"View text does not contain Expected value.");
        }

        [SetUp]
        public static void SetUp()
        {
            Setup();
        }

        [TearDown]
        public static void ClassCleanup()
        {
            TearDown();
        }
    }
}