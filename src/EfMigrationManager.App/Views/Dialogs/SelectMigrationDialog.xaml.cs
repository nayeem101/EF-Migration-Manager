namespace EfMigrationManager.App.Views.Dialogs;

using System.Collections.Generic;
using System.Windows;
using EfMigrationManager.Core.Models;

public partial class SelectMigrationDialog
{
    public MigrationEntry? Selected { get; private set; }

    public SelectMigrationDialog(IEnumerable<MigrationEntry> migrations)
    {
        InitializeComponent();
        MigrationList.ItemsSource = migrations;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Selected = MigrationList.SelectedItem as MigrationEntry;
        DialogResult = Selected is not null;
        if (DialogResult == true) Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void MigrationList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => Ok_Click(sender, new RoutedEventArgs());
}
