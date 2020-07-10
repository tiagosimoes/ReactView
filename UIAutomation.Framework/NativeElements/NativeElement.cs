using SikuliSharp;
using UIAutomation.Framework.Services;
using UIAutomation.Framework.Utils;

namespace UIAutomation.Framework.NativeElements
{
    public class NativeElement
    {
        private readonly IPattern pattern;
        private readonly string name;
        private readonly string pathToPattern;

        public NativeElement(string pathToPattern, string name)
        {
            this.pathToPattern = pathToPattern;
            pattern = Patterns.FromFile(pathToPattern, 0.9f);
            this.name = name;
        }

        public bool IsExist(bool moveCursorOutside = false)
        {
            Logger.Instance.Info($"Is {name} NativeElement Exist (pattern path: {pathToPattern})");
            if (moveCursorOutside)
            {
                MoveCursorOutside();
            }

            return NativeService.GetInstance().session.Exists(pattern);
        }

        public bool Click()
        {
            Logger.Instance.Info($"Clicking {name} NativeElement (pattern path: {pathToPattern})");
            return NativeService.GetInstance().session.Click(pattern);
        }

        public bool WaitForExist(float secondsTimeout = 0)
        {
            Logger.Instance.Info($"Waiting for {name} NativeElement to exist (pattern path: {pathToPattern})");
            return NativeService.GetInstance().session.Wait(pattern, secondsTimeout);
        }

        private void MoveCursorOutside()
        {
            NativeService.GetInstance().session.Hover(pattern, new Point(300, 300));
        }
    }
}
