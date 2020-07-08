using SikuliSharp;

namespace UIAutomation.Framework.NativeElements
{
    public class NativeElement
    {
        private readonly IPattern pattern;

        public NativeElement(string pathToPattern)
        {
            pattern = Patterns.FromFile(pathToPattern, 0.9f);
        }

        public bool IsExist(bool moveCursorOutside = false)
        {
            if(moveCursorOutside)
            {
                MoveCursorOutside();
            }

            return NativeService.GetInstance().session.Exists(pattern);
        }

        public bool Click()
        {
            return NativeService.GetInstance().session.Click(pattern);
        }

        public bool WaitForExist(float secondsTimeout = 0)
        {
            return NativeService.GetInstance().session.Wait(pattern, secondsTimeout);
        }

        private void MoveCursorOutside()
        {
            NativeService.GetInstance().session.Hover(pattern, new Point(300, 300));
        }
    }
}
