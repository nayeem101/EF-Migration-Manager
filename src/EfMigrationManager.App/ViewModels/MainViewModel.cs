namespace EfMigrationManager.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public sealed partial class MainViewModel : ObservableObject
{
    public SolutionPanelViewModel  SolutionPanel  { get; }
    public MigrationPanelViewModel MigrationPanel { get; }
    public TerminalViewModel       Terminal       { get; }

    public MainViewModel(
        SolutionPanelViewModel  solutionPanel,
        MigrationPanelViewModel migrationPanel,
        TerminalViewModel       terminal)
    {
        SolutionPanel  = solutionPanel;
        MigrationPanel = migrationPanel;
        Terminal       = terminal;

        SolutionPanel.SolutionLoaded += MigrationPanel.OnSolutionLoaded;
    }
}
