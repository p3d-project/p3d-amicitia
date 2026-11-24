using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace Amicitia
{
    /// <summary>
    /// Stores user options in %AppData%\Amicitia\settings.ini as simple key=value lines.
    /// 
    /// This deliberately doesn't use the .NET user settings (Settings.settings), those are stored in a folder
    /// named after the assembly version so every release would start over with default options.
    /// Keys this version doesn't know about are kept and written back, so options added by a newer version
    /// aren't lost if an older one is run, and missing keys just fall back to their defaults.
    /// </summary>
    internal static class AppSettings
    {
        // option keys
        public const string ShowCollision = "ShowCollision";
        public const string PlaceMissingModels = "PlaceMissingModels";
        public const string BackgroundColor = "BackgroundColor";

        private static readonly string sDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amicitia");

        private static readonly string sFilePath = Path.Combine(sDirectory, "settings.ini");

        private static readonly Dictionary<string, string> sValues = Load();

        private static Dictionary<string, string> Load()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (File.Exists(sFilePath))
                {
                    foreach (var line in File.ReadAllLines(sFilePath))
                    {
                        int split = line.IndexOf('=');
                        if (split <= 0 || line.StartsWith("#") || line.StartsWith(";"))
                            continue;

                        values[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                // a broken settings file must never stop the app from starting
                Console.WriteLine("Failed to load settings: " + ex.Message);
            }

            return values;
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(sDirectory);

                var lines = new List<string>();
                foreach (var pair in sValues)
                    lines.Add(pair.Key + "=" + pair.Value);

                File.WriteAllLines(sFilePath, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save settings: " + ex.Message);
            }
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            if (sValues.TryGetValue(key, out string text) && bool.TryParse(text, out bool value))
                return value;

            return defaultValue;
        }

        public static void SetBool(string key, bool value)
        {
            sValues[key] = value ? "true" : "false";
            Save();
        }

        /// <summary>
        /// Gets a saved color, or null if none has been saved.
        /// </summary>
        public static Color? GetColor(string key)
        {
            if (sValues.TryGetValue(key, out string text) &&
                int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int argb))
                return Color.FromArgb(argb);

            return null;
        }

        public static void SetColor(string key, Color value)
        {
            sValues[key] = value.ToArgb().ToString("X8", CultureInfo.InvariantCulture);
            Save();
        }
    }
}
