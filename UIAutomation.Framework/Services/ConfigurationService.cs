using UIAutomation.Framework.Objects;
using UIAutomation.Framework.Utils;

namespace UIAutomation.Framework.Services
{
    public class ConfigurationService
    {

        private static ConfigurationService instance;
        public readonly Configuration configuration;

        private ConfigurationService()
        {
            configuration = ResourceUtil.ReadConfig();
        }

        public static Configuration GetConfiguration()
        {
            if (instance == null)
            {
                instance = new ConfigurationService();
            }

            return instance.configuration;
        }
    }
}
