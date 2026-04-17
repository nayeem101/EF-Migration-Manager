namespace EfMigrationManager.Core.Models;

public sealed class AppSettings
{
    public List<string>                          RecentSolutions  { get; set; } = [];
    public Dictionary<string, SolutionSettings> PerSolution      { get; set; } = [];
    public AppearanceSettings                    Appearance       { get; set; } = new();
    public WindowSettings                        Window           { get; set; } = new();
    public bool                                  ShowAllProjects  { get; set; }
}

public sealed class WindowSettings
{
    public double Left        { get; set; } = double.NaN;
    public double Top         { get; set; } = double.NaN;
    public double Width       { get; set; } = 1280;
    public double Height      { get; set; } = 800;
    public bool   IsMaximized { get; set; }
}

public sealed class SolutionSettings
{
    public string? LastStartupProjectPath    { get; set; }
    public string? LastMigrationsProjectPath { get; set; }
    public string? LastContextName           { get; set; }
}

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "Dark";
}
