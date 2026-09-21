using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Returns the directory containing the running executable.
    /// Handles single-file publish correctly: AppDomain.CurrentDomain.BaseDirectory
    /// points to the .NET extraction temp folder, while Environment.ProcessPath
    /// always points to where the user actually placed the exe.
    /// </summary>
    private static string ExeDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath)
        ?? AppDomain.CurrentDomain.BaseDirectory;

    public static AppSettings Load()
    {
        try
        {
            var path = Path.Combine(ExeDirectory, FileName);

            if (!File.Exists(path))
            {
                // Write a default file so the user has a template to edit.
                var defaults = new AppSettings();
                var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never
                });
                File.WriteAllText(path, json);
                return defaults;
            }

            var raw = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(raw);
            return settings ?? new AppSettings();
        }
        catch
        {
            // Fall back to defaults on any error.
            return new AppSettings();
        }
    }
}

