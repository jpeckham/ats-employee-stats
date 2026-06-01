using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AtsEmployeeStats.Maui.Presentation;

internal sealed record DetailMetricPresentation(string Label, string Value);

internal sealed record DetailRowPresentation(
    string Name,
    string PrimaryText,
    string SecondaryText,
    string MetaText = "",
    string? ActionRoute = null,
    string ActionText = "Open")
{
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionRoute);
}

internal sealed record DetailSectionPresentation(
    string Title,
    IReadOnlyList<DetailRowPresentation> Rows);

internal sealed record DetailScreenPresentation(
    string Title,
    string Subtitle,
    IReadOnlyList<DetailMetricPresentation> Metrics,
    IReadOnlyList<DetailSectionPresentation> Sections,
    string StatusText = "Loaded");

internal interface IDetailPresentationTarget
{
    void ShowLoading(string title);

    void ShowDetail(DetailScreenPresentation presentation);

    void ShowMissing(string title, string message);

    void ShowError(string title, string message);
}

internal sealed class DetailPageModel : IDetailPresentationTarget, INotifyPropertyChanged
{
    private string _title = "Loading";
    private string _subtitle = string.Empty;
    private string _statusText = "Loading local statistics...";
    private bool _isBusy;
    private bool _hasContent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DetailMetricPresentation> Metrics { get; } = [];

    public ObservableCollection<DetailSectionPresentation> Sections { get; } = [];

    public string Title
    {
        get => _title;
        private set
        {
            _title = value;
            OnChanged();
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        private set
        {
            _subtitle = value;
            OnChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnChanged();
            OnChanged(nameof(HasNoContent));
        }
    }

    public bool HasContent
    {
        get => _hasContent;
        private set
        {
            _hasContent = value;
            OnChanged();
            OnChanged(nameof(HasNoContent));
        }
    }

    public bool HasNoContent => !HasContent && !IsBusy;

    public void ShowLoading(string title)
    {
        Title = title;
        Subtitle = string.Empty;
        StatusText = "Loading local statistics...";
        IsBusy = true;
        HasContent = false;
        Metrics.Clear();
        Sections.Clear();
    }

    public void ShowDetail(DetailScreenPresentation presentation)
    {
        Title = presentation.Title;
        Subtitle = presentation.Subtitle;
        StatusText = presentation.StatusText;
        IsBusy = false;
        HasContent = true;
        Replace(Metrics, presentation.Metrics);
        Replace(Sections, presentation.Sections);
    }

    public void ShowMissing(string title, string message)
    {
        Title = title;
        Subtitle = message;
        StatusText = message;
        IsBusy = false;
        HasContent = false;
        Metrics.Clear();
        Sections.Clear();
    }

    public void ShowError(string title, string message)
    {
        Title = title;
        Subtitle = message;
        StatusText = message;
        IsBusy = false;
        HasContent = false;
        Metrics.Clear();
        Sections.Clear();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    private void OnChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
