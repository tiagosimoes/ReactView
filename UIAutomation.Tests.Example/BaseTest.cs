using NUnit.Framework;
using UIAutomation.Framework;
using UIAutomation.Framework.Utils;

namespace UIAutomation.Tests.Example
{
    public class BaseTest : AppSession
    {
        [SetUp]
        public static void SetUp()
        {
            Logger.Instance.Info($" ------------- Starting Test {TestContext.CurrentContext.Test.Name} ------------- ");
            Setup();
        }

        [TearDown]
        public static void ClassCleanup()
        {
            Logger.Instance.Info($" ------------- Test {TestContext.CurrentContext.Test.Name} was Finished ------------- ");
            TearDown();
        }
    }
}
