using System.Globalization;
using System.Windows;

namespace AtsEmployeeStats.Wpf.Services;

public sealed class SourceWizardConfirmation : ISourceWizardConfirmation
{
    public bool ConfirmDatabaseBuild(DatabaseDiskSpaceEstimate estimate)
    {
        var result = MessageBox.Show(
            $"Building the Employee Database takes at least {FormatBytes(estimate.ProjectedDatabaseBytes)} of total space." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"This configuration needs {FormatBytes(estimate.RequiredAdditionalBytes)} more free space." +
            $"{Environment.NewLine}" +
            $"You have {FormatBytes(estimate.FreeBytes)} free." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Click OK to save this configuration and build the database.",
            "Confirm Employee Database Build",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.OK;
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{value:N0} {units[unit]}")
            : string.Create(CultureInfo.InvariantCulture, $"{value:N1} {units[unit]}");
    }
}
