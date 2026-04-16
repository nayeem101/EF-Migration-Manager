namespace EfMigrationManager.App.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EfMigrationManager.Core.Models;
using EfMigrationManager.Core.Services;
using Microsoft.Win32;

public sealed partial class SolutionPanelViewModel : ObservableObject
{
    private readonly ISolutionParserService _parser;
    private readonly ISettingsService       _settings;

    [ObservableProperty] private SolutionInfo? _currentSolution;
    [ObservableProperty] private bool          _isLoading;
    [ObservableProperty] private string?       _errorMessage;

    public ObservableCollection<string>      RecentSolutions { get; } = [];
    public ObservableCollection<ProjectInfo> EfProjects      { get; } = [];

    public event Action<SolutionInfo>? SolutionLoaded;

    public SolutionPanelViewModel(
        ISolutionParserService parser,
        ISettingsService       settings)
    {
        _parser   = parser;
        _settings = settings;

        foreach (var s in settings.Settings.RecentSolutions)
            RecentSolutions.Add(s);
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title       = "Open Solution",
            Filter      = "Solution Files|*.sln;*.slnx|Legacy Solution (*.sln)|*.sln|XML Solution (*.slnx)|*.slnx",
            FilterIndex = 1,
        };

        if (dialog.ShowDialog() != true) return;
        await LoadSolutionAsync(dialog.FileName);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string path)
        => await LoadSolutionAsync(path);

    private async Task LoadSolutionAsync(string path)
    {
        ErrorMessage = null;
        IsLoading    = true;

        try
        {
            var solution = await _parser.ParseAsync(path);
            CurrentSolution = solution;

            EfProjects.Clear();
            foreach (var p in solution.Projects.Where(p => p.HasEfCore))
                EfProjects.Add(p);

            _settings.AddRecentSolution(path);
            RefreshRecentSolutions();

            SolutionLoaded?.Invoke(solution);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load solution: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshRecentSolutions()
    {
        RecentSolutions.Clear();
        foreach (var s in _settings.Settings.RecentSolutions)
            RecentSolutions.Add(s);
    }
}
