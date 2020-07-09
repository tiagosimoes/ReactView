using SikuliSharp;

namespace UIAutomation.Framework.Services
{
    public class NativeService
    {
        private static NativeService instance;
        public ISikuliSession session;

        public static NativeService GetInstance()
        {
            if (instance == null)
            {
                instance = new NativeService();
            }

            return instance;
        }

        private NativeService()
        {
            session = Sikuli.CreateSession();
        }
    }
}
