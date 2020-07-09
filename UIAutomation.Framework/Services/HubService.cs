
using System.Diagnostics;

namespace UIAutomation.Framework.Services
{
    public class HubService
    {
        private static HubService instance;
        private Process process;

        private HubService()
        {
            process = Process.Start(ConfigurationService.GetConfiguration().WinAppDriverPath);
        }

        public static HubService GetInstance()
        {
            if (instance == null)
            {
                instance = new HubService();
            }

            return instance;
        }

        public void Dispose()
        {
            process.Close();
        }
    }
}
