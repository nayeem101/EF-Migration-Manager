namespace EfMigrationManager.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EfMigrationManager.Core.Helpers;
using EfMigrationManager.Core.Models;
using EfMigrationManager.Core.Services;

public sealed partial class TerminalViewModel : ObservableObject
{
    private readonly IProcessRunnerService _runner;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _lastCommand = string.Empty;

    public ObservableCollection<TerminalLine> Lines { get; } = [];

    private string _lastWorkingDirectory = string.Empty;

    public TerminalViewModel(IProcessRunnerService runner)
    {
        _runner = runner;
    }

    public void AppendLine(ProcessLine line)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var color = AnsiHelper.ExtractColor(line.Text);
            var clean = AnsiHelper.Strip(line.Text);

            if (string.IsNullOrWhiteSpace(clean) && Lines.Count > 0
                && string.IsNullOrWhiteSpace(Lines.Last().Text))
                return;

            Lines.Add(new TerminalLine
            {
                Text      = clean,
                ColorHint = color ?? (line.Kind == ProcessLineKind.StdErr ? "Red" : null),
                IsSystem  = line.Kind == ProcessLineKind.System
            });
        });
    }

    public void BeginCommand(string command, string workingDirectory)
    {
        LastCommand           = command;
        _lastWorkingDirectory = workingDirectory;
        IsRunning             = true;

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Lines.Count > 0)
                Lines.Add(new TerminalLine { Text = new string('─', 60), IsSystem = true });
        });
    }

    public void EndCommand() => IsRunning = false;

    [RelayCommand]
    private void Clear() => Lines.Clear();

    [RelayCommand]
    private void CopyAll()
    {
        var text = string.Join(Environment.NewLine, Lines.Select(l => l.Text));
        Clipboard.SetText(text);
    }

    [RelayCommand]
    private void OpenInTerminal()
        => _runner.OpenInTerminal(LastCommand, _lastWorkingDirectory);
}

public sealed class TerminalLine
{
    public required string  Text      { get; init; }
    public string?          ColorHint { get; init; }
    public bool             IsSystem  { get; init; }
}
