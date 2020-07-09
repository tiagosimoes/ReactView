using System;
using System.IO;
using System.Text.Json;
using UIAutomation.Framework.Objects;

namespace UIAutomation.Framework.Utils
{
    public static class ResourceUtil
    {
        public static string PatternPath(string relativePath)
        {
            var binPath = GetBinPath();
            var dataFolder = Path.Combine(binPath, "ImageLocators");
            return Path.Combine(dataFolder, relativePath);
        }

        public static Configuration ReadConfig()
        {
            var binPath = GetBinPath();
            var jsonString = File.ReadAllText(Path.Combine(binPath, "config.json"));
            return JsonSerializer.Deserialize<Configuration>(jsonString);
        }

        private static string GetBinPath()
        {
            return Path.GetDirectoryName(new Uri(typeof(ResourceUtil).Assembly.CodeBase).LocalPath);
        }
    }
}
