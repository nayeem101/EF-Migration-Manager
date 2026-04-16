namespace EfMigrationManager.Core.Helpers;

public static class PathHelper
{
    public static string ToRelativeDisplay(string absolutePath, string basePath)
    {
        var rel = Path.GetRelativePath(basePath, absolutePath);
        return rel.StartsWith("..") ? absolutePath : rel;
    }

    public static bool IsSolutionFile(string path)
        => Path.GetExtension(path).ToLowerInvariant() is ".sln" or ".slnx";
}
