using OpenQA.Selenium;
using System;
using System.IO;

namespace UIAutomation.Framework.Extensions
{
    public static class ResourceUtil
    {
        public static string PatternPath(string relativePath)
        {
            var binPath = Path.GetDirectoryName(new Uri(typeof(ResourceUtil).Assembly.CodeBase).LocalPath);
            var dataFolder = Path.Combine(binPath, "ImageLocators");
            return Path.Combine(dataFolder, relativePath);
        }
    }
}
