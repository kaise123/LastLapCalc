using System;
using System.IO;
using System.Text.Json;

namespace LastLapCalc.Configuration;

public sealed class AppSettings
{
    public int RaceDurationMinutes { get; set; } = 30;
    public string XmlFilePath { get; set; } = "race-data.xml";
    public int AverageLapWindow { get; set; } = 3;
    public int RefreshIntervalMs { get; set; } = 500;
}

public static class AppSettingsLoader
{
    private const string FileName = "appsettings.json";

    public static AppSettings Load()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, FileName);

            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings ?? new AppSettings();
        }
        catch
        {
            // Fall back to defaults on any error.
            return new AppSettings();
        }
    }
}

