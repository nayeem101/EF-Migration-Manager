namespace EfMigrationManager.App.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EfMigrationManager.Core.Helpers;
using EfMigrationManager.Core.Models;
using EfMigrationManager.Core.Services;

public sealed partial class MigrationPanelViewModel : ObservableObject
{
    private readonly IEfDiscoveryService      _discovery;
    private readonly IEfCommandBuilderService _cmdBuilder;
    private readonly IProcessRunnerService    _runner;
    private readonly ISettingsService         _settings;
    private readonly TerminalViewModel        _terminal;

    [ObservableProperty] private SolutionInfo?   _currentSolution;
    [ObservableProperty] private ProjectInfo?    _selectedStartupProject;
    [ObservableProperty] private ProjectInfo?    _selectedMigrationsProject;
    [ObservableProperty] private DbContextInfo?  _selectedContext;

    [ObservableProperty] private bool   _isLoadingContexts;
    [ObservableProperty] private bool   _isLoadingMigrations;
    [ObservableProperty] private bool   _isExecuting;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private AppNotification? _notification;

    public ObservableCollection<ProjectInfo>    StartupProjects   { get; } = [];
    public ObservableCollection<ProjectInfo>    MigrationProjects { get; } = [];
    public ObservableCollection<DbContextInfo>  Contexts          { get; } = [];
    public ObservableCollection<MigrationEntry> Migrations        { get; } = [];

    private CancellationTokenSource? _cts;

    public MigrationPanelViewModel(
        IEfDiscoveryService      discovery,
        IEfCommandBuilderService cmdBuilder,
        IProcessRunnerService    runner,
        ISettingsService         settings,
        TerminalViewModel        terminal)
    {
        _discovery  = discovery;
        _cmdBuilder = cmdBuilder;
        _runner     = runner;
        _settings   = settings;
        _terminal   = terminal;
    }

    partial void OnSelectedStartupProjectChanged(ProjectInfo? value)
    {
        PersistCurrentSelections();
        RefreshContextsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMigrationsProjectChanged(ProjectInfo? value)
    {
        PersistCurrentSelections();
        RefreshContextsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedContextChanged(DbContextInfo? value)
    {
        PersistCurrentSelections();
        if (value is not null) _ = RefreshMigrationsAsync();
        RefreshMigrationsCommand.NotifyCanExecuteChanged();
    }

    public void OnSolutionLoaded(SolutionInfo solution)
    {
        CurrentSolution = solution;

        StartupProjects.Clear();
        MigrationProjects.Clear();
        Contexts.Clear();
        Migrations.Clear();

        var efProjects        = solution.Projects.Where(p => p.HasEfCore).ToList();
        var startupCandidates = efProjects.Where(p => p.IsExecutable).ToList();
        if (startupCandidates.Count == 0) startupCandidates = efProjects;

        foreach (var p in startupCandidates) StartupProjects.Add(p);
        foreach (var p in efProjects)        MigrationProjects.Add(p);

        var saved = _settings.GetSolutionSettings(solution.Path);

        SelectedStartupProject = StartupProjects
            .FirstOrDefault(p => p.AbsolutePath == saved.LastStartupProjectPath)
            ?? StartupProjects.FirstOrDefault();

        SelectedMigrationsProject = MigrationProjects
            .FirstOrDefault(p => p.AbsolutePath == saved.LastMigrationsProjectPath)
            ?? MigrationProjects.FirstOrDefault(p => p.HasEfDesign)
            ?? MigrationProjects.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshContextsAsync()
    {
        if (BuildOptions() is not { } opts) return;

        IsLoadingContexts = true;
        Contexts.Clear();
        SelectedContext = null;
        Notification = null;

        try
        {
            var contexts = await _discovery.ListDbContextsAsync(opts);
            foreach (var c in contexts) Contexts.Add(c);

            var saved = _settings.GetSolutionSettings(CurrentSolution!.Path);
            SelectedContext = Contexts.FirstOrDefault(c => c.FullName == saved.LastContextName)
                              ?? Contexts.FirstOrDefault();
        }
        catch (EfDiscoveryException ex)
        {
            Notification = ex.Notification;
            StatusMessage = ex.Notification.Title;
        }
        catch (Exception ex)
        {
            Notification = AppNotification.Error("Discovery failed", ex.Message);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingContexts = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshMigrations))]
    private async Task RefreshMigrationsAsync()
    {
        if (BuildOptions() is not { } opts) return;

        IsLoadingMigrations = true;
        Migrations.Clear();

        try
        {
            var migrations = await _discovery.ListMigrationsAsync(opts);
            foreach (var m in migrations) Migrations.Add(m);
        }
        catch (EfDiscoveryException ex)
        {
            Notification = ex.Notification;
            StatusMessage = ex.Notification.Title;
        }
        catch (Exception ex)
        {
            Notification = AppNotification.Error("Listing failed", ex.Message);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingMigrations = false;
        }
    }

    private bool CanRefresh() =>
        SelectedStartupProject is not null && SelectedMigrationsProject is not null;

    private bool CanRefreshMigrations() =>
        CanRefresh() && SelectedContext is not null;

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task AddMigrationAsync(string migrationName)
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.AddMigration(opts, migrationName);
        await ExecuteEfCommandAsync(args, opts);
        await RefreshMigrationsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task RemoveMigrationAsync()
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.RemoveMigration(opts);
        await ExecuteEfCommandAsync(args, opts);
        await RefreshMigrationsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task UpdateDatabaseAsync(string? targetMigration = null)
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.UpdateDatabase(opts, targetMigration);
        await ExecuteEfCommandAsync(args, opts);
        await RefreshMigrationsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task DropDatabaseAsync()
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.DropDatabase(opts);
        await ExecuteEfCommandAsync(args, opts);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task GenerateScriptAsync((string? FromMigration, string? ToMigration, string? OutputPath) p)
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.GenerateScript(opts, p.FromMigration, p.ToMigration, p.OutputPath);
        await ExecuteEfCommandAsync(args, opts);
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusMessage = "Cancelling...";
    }

    [RelayCommand]
    private void DismissNotification() => Notification = null;

    [RelayCommand]
    private void NotificationAction()
    {
        if (Notification?.ActionClipboardText is { } clip)
            System.Windows.Clipboard.SetText(clip);
    }

    private bool CanExecuteCommand()
        => CanRefreshMigrations() && !IsExecuting;

    private async Task ExecuteEfCommandAsync(string args, EfCommandOptions opts)
    {
        IsExecuting = true;
        StatusMessage = null;
        Notification = null;
        _cts = new CancellationTokenSource();

        _terminal.BeginCommand(_cmdBuilder.FormatForDisplay(args), opts.WorkingDirectory);

        var aggregate = new System.Text.StringBuilder();
        try
        {
            await foreach (var line in _runner.RunAsync("dotnet", args, opts.WorkingDirectory, _cts.Token))
            {
                _terminal.AppendLine(line);
                aggregate.AppendLine(line.Text);
            }

            if (EfErrorDetector.Detect(aggregate.ToString()) is { } err)
            {
                Notification = err;
                StatusMessage = err.Title;
            }
            else
            {
                StatusMessage = "Completed successfully.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _terminal.EndCommand();
            IsExecuting = false;
            _cts?.Dispose();
            _cts = null;
        }

        AddMigrationCommand.NotifyCanExecuteChanged();
        RemoveMigrationCommand.NotifyCanExecuteChanged();
        UpdateDatabaseCommand.NotifyCanExecuteChanged();
        DropDatabaseCommand.NotifyCanExecuteChanged();
        GenerateScriptCommand.NotifyCanExecuteChanged();
    }

    private EfCommandOptions? BuildOptions()
    {
        if (CurrentSolution is null
            || SelectedStartupProject is null
            || SelectedMigrationsProject is null
            || SelectedContext is null)
            return null;

        return new EfCommandOptions
        {
            StartupProjectPath    = SelectedStartupProject.AbsolutePath,
            MigrationsProjectPath = SelectedMigrationsProject.AbsolutePath,
            ContextName           = SelectedContext.FullName,
            WorkingDirectory      = CurrentSolution.DirectoryPath
        };
    }

    private void PersistCurrentSelections()
    {
        if (CurrentSolution is null) return;
        _settings.SaveSolutionSettings(CurrentSolution.Path, new()
        {
            LastStartupProjectPath    = SelectedStartupProject?.AbsolutePath,
            LastMigrationsProjectPath = SelectedMigrationsProject?.AbsolutePath,
            LastContextName           = SelectedContext?.FullName
        });
    }
}
