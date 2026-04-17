namespace EfMigrationManager.App.Views;

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EfMigrationManager.App.ViewModels;
using EfMigrationManager.App.Views.Dialogs;
using EfMigrationManager.Core.Models;
using EfMigrationManager.Core.Services;
using Wpf.Ui.Appearance;

public partial class MainWindow
{
    private readonly MainViewModel    _vm;
    private readonly ISettingsService _settings;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        _vm       = viewModel;
        _settings = settings;
        DataContext = viewModel;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var w = _settings.Settings.Window;
        if (!double.IsNaN(w.Left) && !double.IsNaN(w.Top))
        {
            Left   = Math.Max(0, w.Left);
            Top    = Math.Max(0, w.Top);
        }
        if (w.Width  > 200) Width  = w.Width;
        if (w.Height > 200) Height = w.Height;
        if (w.IsMaximized) WindowState = WindowState.Maximized;

        var darkTheme = !string.Equals(_settings.Settings.Appearance.Theme, "Light", StringComparison.OrdinalIgnoreCase);
        ThemeToggle.IsChecked = darkTheme;
        ApplicationThemeManager.Apply(darkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckDotnetEfAsync();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var w = _settings.Settings.Window;
        w.IsMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            w.Left   = Left;
            w.Top    = Top;
            w.Width  = Width;
            w.Height = Height;
        }
        _settings.Settings.Appearance.Theme = ThemeToggle.IsChecked == true ? "Dark" : "Light";
        _settings.Save();
    }

    private void ThemeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var dark = ThemeToggle.IsChecked == true;
        ApplicationThemeManager.Apply(dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    private async Task CheckDotnetEfAsync()
    {
        var dotnetOk = await RunProbeAsync("dotnet", "--version");
        if (!dotnetOk)
        {
            ShowStartupWarning("dotnet SDK not detected",
                "The .NET SDK does not appear to be on PATH. Install the .NET SDK from https://dot.net.");
            return;
        }

        var efOk = await RunProbeAsync("dotnet", "ef --version");
        if (!efOk)
        {
            ShowStartupWarning("dotnet-ef not installed",
                "Run: dotnet tool install --global dotnet-ef");
        }
    }

    private static Task<bool> RunProbeAsync(string exe, string args)
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
                using var p = Process.Start(psi);
                if (p is null) return false;
                p.WaitForExit(5000);
                return p.HasExited && p.ExitCode == 0;
            }
            catch { return false; }
        });
    }

    private void ShowStartupWarning(string title, string message)
    {
        StartupInfoBar.Title    = title;
        StartupInfoBar.Message  = message;
        StartupInfoBar.Severity = Wpf.Ui.Controls.InfoBarSeverity.Warning;
        StartupInfoBar.IsOpen   = true;
    }

    private async void RecentSolution_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: string path })
            await _vm.SolutionPanel.OpenRecentCommand.ExecuteAsync(path);
    }

    private async void AddMigration_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("Add Migration", "Migration name:")
        {
            Owner = this,
            Validator = ValidateMigrationName
        };
        if (dlg.ShowDialog() == true)
            await _vm.MigrationPanel.AddMigrationCommand.ExecuteAsync(dlg.Value);
    }

    private async void RemoveMigration_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Remove the last migration? If it has been applied to the database, this will fail unless you revert first.",
            "Remove Migration", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (result == MessageBoxResult.OK)
            await _vm.MigrationPanel.RemoveMigrationCommand.ExecuteAsync(null);
    }

    private async void UpdateTo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SelectMigrationDialog(_vm.MigrationPanel.Migrations) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Selected is not null)
            await _vm.MigrationPanel.UpdateDatabaseCommand.ExecuteAsync(dlg.Selected.Name);
    }

    private async void GenerateScript_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new GenerateScriptDialog(_vm.MigrationPanel.Migrations) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            await _vm.MigrationPanel.GenerateScriptCommand.ExecuteAsync(
                (dlg.FromMigration, dlg.ToMigration, dlg.OutputPath));
        }
    }

    private async void DropDatabase_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog(
            "Drop Database",
            "This will permanently drop the database. Type DROP to confirm:")
        {
            Owner = this,
            Validator = v => v == "DROP" ? null : "Type DROP (uppercase) to confirm."
        };
        if (dlg.ShowDialog() == true)
            await _vm.MigrationPanel.DropDatabaseCommand.ExecuteAsync(null);
    }

    private async void UpdateToMigration_Click(object sender, RoutedEventArgs e)
    {
        if (MigrationsListView.SelectedItem is MigrationEntry m)
            await _vm.MigrationPanel.UpdateDatabaseCommand.ExecuteAsync(m.Name);
    }

    private void CopyMigrationName_Click(object sender, RoutedEventArgs e)
    {
        if (MigrationsListView.SelectedItem is MigrationEntry { SafeName: { } name })
            Clipboard.SetText(name);
    }

    private void CopyMigrationId_Click(object sender, RoutedEventArgs e)
    {
        if (MigrationsListView.SelectedItem is MigrationEntry m)
            Clipboard.SetText(m.Name);
    }

    // -------- Tree right-click handlers --------

    private static ProjectInfo? GetNodeProject(object sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SolutionNode n && n.Project is { } proj)
            return proj;
        if (sender is MenuItem mi && mi.Parent is ContextMenu cm
            && cm.PlacementTarget is FrameworkElement pt && pt.Tag is SolutionNode node && node.Project is { } p)
            return p;
        return null;
    }

    private void Tree_Node_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is SolutionNode node && node.Project is { } proj)
        {
            _ = _vm.MigrationPanel.SelectMigrationsProjectAsync(proj);
        }
    }

    private async void Tree_SetAsMigrations_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeProject(sender) is { } p)
            await _vm.MigrationPanel.SelectMigrationsProjectAsync(p);
    }

    private void Tree_SetAsStartup_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeProject(sender) is { } p)
            _vm.MigrationPanel.SetStartupProject(p);
    }

    private async void Tree_AddMigration_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeProject(sender) is not { } p) return;
        await _vm.MigrationPanel.SelectMigrationsProjectAsync(p);
        AddMigration_Click(sender, e);
    }

    private async void Tree_UpdateLatest_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeProject(sender) is not { } p) return;
        await _vm.MigrationPanel.SelectMigrationsProjectAsync(p);
        if (_vm.MigrationPanel.UpdateDatabaseCommand.CanExecute(null))
            await _vm.MigrationPanel.UpdateDatabaseCommand.ExecuteAsync(null);
    }

    private async void Tree_UpdateTo_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeProject(sender) is not { } p) return;
        await _vm.MigrationPanel.SelectMigrationsProjectAsync(p);
        UpdateTo_Click(sender, e);
    }

    private async void Tree_RemoveMigration_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeProject(sender) is not { } p) return;
        await _vm.MigrationPanel.SelectMigrationsProjectAsync(p);
        RemoveMigration_Click(sender, e);
    }

    private async void Tree_GenerateScript_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeProject(sender) is not { } p) return;
        await _vm.MigrationPanel.SelectMigrationsProjectAsync(p);
        GenerateScript_Click(sender, e);
    }

    private async void Tree_DropDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeProject(sender) is not { } p) return;
        await _vm.MigrationPanel.SelectMigrationsProjectAsync(p);
        DropDatabase_Click(sender, e);
    }

    private static string? ValidateMigrationName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Name required.";
        if (name.Length > 100) return "Too long (max 100).";
        if (char.IsDigit(name[0])) return "Must not start with a digit.";
        foreach (var c in name)
            if (!char.IsLetterOrDigit(c) && c != '_')
                return "Only letters, digits, and underscores allowed.";
        return null;
    }
}
