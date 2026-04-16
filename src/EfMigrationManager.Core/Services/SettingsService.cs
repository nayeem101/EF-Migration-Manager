namespace EfMigrationManager.Core.Services;

using System.Text.Json;
using EfMigrationManager.Core.Models;
using Microsoft.Extensions.Logging;

public sealed class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly ILogger<SettingsService> _logger;
    private const int MaxRecentSolutions = 10;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; }

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "EfMigrationManager");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        Settings = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new AppSettings();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void AddRecentSolution(string path)
    {
        Settings.RecentSolutions.Remove(path);
        Settings.RecentSolutions.Insert(0, path);
        if (Settings.RecentSolutions.Count > MaxRecentSolutions)
            Settings.RecentSolutions.RemoveRange(MaxRecentSolutions,
                Settings.RecentSolutions.Count - MaxRecentSolutions);
        Save();
    }

    public void SaveSolutionSettings(string solutionPath, SolutionSettings settings)
    {
        Settings.PerSolution[solutionPath] = settings;
        Save();
    }

    public SolutionSettings GetSolutionSettings(string solutionPath)
        => Settings.PerSolution.TryGetValue(solutionPath, out var s) ? s : new SolutionSettings();
}
