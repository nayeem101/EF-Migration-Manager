namespace EfMigrationManager.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public sealed partial class MainViewModel : ObservableObject
{
    public SolutionPanelViewModel  SolutionPanel  { get; }
    public MigrationPanelViewModel MigrationPanel { get; }
    public TerminalViewModel       Terminal       { get; }
    public SolutionTreeViewModel   SolutionTree   { get; }

    public MainViewModel(
        SolutionPanelViewModel  solutionPanel,
        MigrationPanelViewModel migrationPanel,
        TerminalViewModel       terminal,
        SolutionTreeViewModel   solutionTree)
    {
        SolutionPanel  = solutionPanel;
        MigrationPanel = migrationPanel;
        Terminal       = terminal;
        SolutionTree   = solutionTree;

        SolutionPanel.SolutionLoaded += sol =>
        {
            SolutionTree.Build(sol);
            MigrationPanel.OnSolutionLoaded(sol);
        };
    }
}
