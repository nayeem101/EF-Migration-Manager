namespace EfMigrationManager.App.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EfMigrationManager.Core.Models;

public sealed class SolutionNode : ObservableObject
{
    public string Name { get; }
    public bool   IsFolder { get; }
    public ProjectInfo? Project { get; }
    public ObservableCollection<SolutionNode> Children { get; } = [];

    public bool IsMigrationCandidate => Project?.IsMigrationCandidate ?? false;
    public bool IsStartupCandidate   => Project?.IsStartupCandidate   ?? false;
    public bool HasEfCore            => Project?.HasEfCoreTransitive  ?? false;

    public string Icon
    {
        get
        {
            if (IsFolder) return "\uE8B7"; // folder
            if (IsMigrationCandidate) return "\uE8F1"; // database
            if (IsStartupCandidate) return "\uE7C4";   // play
            if (HasEfCore) return "\uEA86";            // link
            return "\uE7C3";                           // doc
        }
    }

    public string Badge
    {
        get
        {
            if (IsFolder) return string.Empty;
            if (IsMigrationCandidate && IsStartupCandidate) return "Startup + Migrations";
            if (IsMigrationCandidate) return "Migrations";
            if (IsStartupCandidate)   return "Startup";
            if (HasEfCore)            return "EF (ref)";
            return string.Empty;
        }
    }

    public SolutionNode(string name, bool isFolder, ProjectInfo? project)
    {
        Name     = name;
        IsFolder = isFolder;
        Project  = project;
    }
}

public sealed partial class SolutionTreeViewModel : ObservableObject
{
    public ObservableCollection<SolutionNode> Roots { get; } = [];

    public void Build(SolutionInfo solution)
    {
        Roots.Clear();

        // group by SolutionFolder path. null folder -> root.
        var folderNodes = new Dictionary<string, SolutionNode>(StringComparer.OrdinalIgnoreCase);

        SolutionNode EnsureFolder(string folderPath)
        {
            if (folderNodes.TryGetValue(folderPath, out var existing)) return existing;

            var parts = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            SolutionNode? parent = null;
            var accum = string.Empty;
            SolutionNode? node = null;

            foreach (var part in parts)
            {
                accum = string.IsNullOrEmpty(accum) ? part : $"{accum}/{part}";
                if (!folderNodes.TryGetValue(accum, out node))
                {
                    node = new SolutionNode(part, isFolder: true, project: null);
                    folderNodes[accum] = node;
                    if (parent is null) Roots.Add(node);
                    else parent.Children.Add(node);
                }
                parent = node;
            }
            return node!;
        }

        foreach (var p in solution.Projects.OrderBy(p => p.SolutionFolder ?? "").ThenBy(p => p.Name))
        {
            var projNode = new SolutionNode(p.Name, isFolder: false, project: p);

            if (string.IsNullOrEmpty(p.SolutionFolder))
                Roots.Add(projNode);
            else
                EnsureFolder(p.SolutionFolder).Children.Add(projNode);
        }
    }
}
