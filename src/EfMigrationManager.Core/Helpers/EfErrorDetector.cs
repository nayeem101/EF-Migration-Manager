namespace EfMigrationManager.Core.Helpers;

using EfMigrationManager.Core.Models;

public static class EfErrorDetector
{
    public static AppNotification? Detect(string aggregatedOutput)
    {
        if (string.IsNullOrWhiteSpace(aggregatedOutput)) return null;

        if (aggregatedOutput.Contains("No executable found matching command", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("Could not execute because the specified command or file was not found", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("'dotnet-ef'", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("dotnet ef is not recognized", StringComparison.OrdinalIgnoreCase))
        {
            return AppNotification.Error(
                "dotnet-ef tools not found",
                "Install with: dotnet tool install --global dotnet-ef",
                actionLabel: "Copy install command",
                actionClip: "dotnet tool install --global dotnet-ef");
        }

        if (aggregatedOutput.Contains("Build FAILED", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("Build failed.", StringComparison.OrdinalIgnoreCase))
        {
            return AppNotification.Error(
                "Build failed",
                "The project failed to build. Check the terminal output for compilation errors.");
        }

        if (aggregatedOutput.Contains("Unable to create an object of type", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("Unable to create a 'DbContext'", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("A connection string", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("No database provider has been configured", StringComparison.OrdinalIgnoreCase))
        {
            return AppNotification.Error(
                "DbContext configuration error",
                "Check appsettings.json and IDesignTimeDbContextFactory in the startup project.");
        }

        if (aggregatedOutput.Contains("Login failed", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("Cannot open database", StringComparison.OrdinalIgnoreCase)
            || aggregatedOutput.Contains("could not connect to server", StringComparison.OrdinalIgnoreCase))
        {
            return AppNotification.Error(
                "Database connection failed",
                "Verify the connection string and that the database server is reachable.");
        }

        return null;
    }
}
