namespace AtsEmployeeStats.Wpf.Services;

public interface ISourceWizardConfirmation
{
    bool ConfirmDatabaseBuild(DatabaseDiskSpaceEstimate estimate);
}
