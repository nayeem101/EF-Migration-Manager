namespace EfMigrationManager.App.Views.Dialogs;

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EfMigrationManager.Core.Models;
using Microsoft.Win32;

public partial class GenerateScriptDialog
{
    public string? FromMigration { get; private set; }
    public string? ToMigration   { get; private set; }
    public string? OutputPath    { get; private set; }
    public bool    Idempotent    { get; private set; } = true;

    public GenerateScriptDialog(IEnumerable<MigrationEntry> migrations)
    {
        InitializeComponent();
        var list = migrations.ToList();
        FromCombo.ItemsSource = list;
        ToCombo.ItemsSource   = list;

        FromCombo.SelectedItem = list.LastOrDefault(m => m.Status == MigrationStatus.Applied);
        ToCombo.SelectedItem   = list.LastOrDefault();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "SQL Script (*.sql)|*.sql|All files|*.*",
            DefaultExt = ".sql",
            FileName = "migration.sql"
        };
        if (dlg.ShowDialog() == true) OutputBox.Text = dlg.FileName;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        FromMigration = (FromCombo.SelectedItem as MigrationEntry)?.Name;
        ToMigration   = (ToCombo.SelectedItem   as MigrationEntry)?.Name;
        OutputPath    = string.IsNullOrWhiteSpace(OutputBox.Text) ? null : OutputBox.Text;
        Idempotent    = IdempotentCheck.IsChecked == true;
        DialogResult  = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
