namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void AddRecentSolution(string path);
    void SaveSolutionSettings(string solutionPath, SolutionSettings settings);
    SolutionSettings GetSolutionSettings(string solutionPath);
}
