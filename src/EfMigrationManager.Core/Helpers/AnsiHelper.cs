namespace EfMigrationManager.Core.Helpers;

using System.Text.RegularExpressions;

public static partial class AnsiHelper
{
    [GeneratedRegex(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled)]
    private static partial Regex AnsiPattern();

    public static string Strip(string input) => AnsiPattern().Replace(input, string.Empty);

    public static string? ExtractColor(string rawLine)
    {
        if (rawLine.Contains("\x1B[31m") || rawLine.Contains("\x1B[91m")) return "Red";
        if (rawLine.Contains("\x1B[33m") || rawLine.Contains("\x1B[93m")) return "Yellow";
        if (rawLine.Contains("\x1B[32m") || rawLine.Contains("\x1B[92m")) return "LimeGreen";
        if (rawLine.Contains("\x1B[36m") || rawLine.Contains("\x1B[96m")) return "Cyan";
        return null;
    }
}
