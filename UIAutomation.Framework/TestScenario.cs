using NUnit.Framework;
using OpenQA.Selenium;

namespace UIAutomation.Framework { 

[TestFixture]
public class TestScenario : AppSession
{
        [Test]
        public void ClickSomething()
        {
            var menuItem = session.FindElementByTagName("MenuItem");
            menuItem.Click();
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