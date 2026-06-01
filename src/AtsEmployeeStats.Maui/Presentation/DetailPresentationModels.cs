using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AtsEmployeeStats.Maui.Presentation;

internal sealed record DetailMetricPresentation(string Label, string Value);

internal sealed record DetailRowPresentation(
    string Name,
    string PrimaryText,
    string SecondaryText,
    string MetaText = "",
    string? ActionRoute = null,
    string ActionText = "Open",
    string SparklineText = "")
{
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionRoute);
}

internal sealed record DetailSectionTabItem(
    string Id,
    string Title,
    bool IsSelected,
    ICommand SelectCommand);

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
    private string _activeSectionId = string.Empty;
    private string _sortColumn = "Primary";
    private bool _sortDescending = true;
    private IReadOnlyList<DetailSectionPresentation> _sections = [];

    public DetailPageModel()
    {
        SelectSectionCommand = new Command<string?>(SelectSection);
        SortSectionRowsCommand = new Command<string?>(SortSectionRows);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DetailMetricPresentation> Metrics { get; } = [];

    public ObservableCollection<DetailSectionPresentation> Sections { get; } = [];

    public ObservableCollection<DetailSectionTabItem> SectionTabs { get; } = [];

    public ObservableCollection<DetailRowPresentation> ActiveSectionRows { get; } = [];

    public ICommand SelectSectionCommand { get; }

    public ICommand SortSectionRowsCommand { get; }

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
        SectionTabs.Clear();
        ActiveSectionRows.Clear();
        _sections = [];
        _activeSectionId = string.Empty;
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
        _sections = presentation.Sections;
        _activeSectionId = _sections.FirstOrDefault()?.Title ?? string.Empty;
        _sortColumn = "Primary";
        _sortDescending = true;
        BuildSectionTabs();
        RefreshActiveSectionRows();
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
        SectionTabs.Clear();
        ActiveSectionRows.Clear();
        _sections = [];
        _activeSectionId = string.Empty;
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
        SectionTabs.Clear();
        ActiveSectionRows.Clear();
        _sections = [];
        _activeSectionId = string.Empty;
    }

    private void SelectSection(string? sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId) ||
            StringComparer.Ordinal.Equals(sectionId, _activeSectionId))
        {
            return;
        }

        _activeSectionId = sectionId;
        _sortColumn = "Primary";
        _sortDescending = true;
        BuildSectionTabs();
        RefreshActiveSectionRows();
    }

    private void SortSectionRows(string? column)
    {
        var nextColumn = string.IsNullOrWhiteSpace(column) ? "Primary" : column;
        if (StringComparer.OrdinalIgnoreCase.Equals(_sortColumn, nextColumn))
            _sortDescending = !_sortDescending;
        else
        {
            _sortColumn = nextColumn;
            _sortDescending = true;
        }

        RefreshActiveSectionRows();
    }

    private void BuildSectionTabs()
    {
        SectionTabs.Clear();
        foreach (var section in _sections)
        {
            SectionTabs.Add(new DetailSectionTabItem(
                section.Title,
                section.Title,
                StringComparer.Ordinal.Equals(section.Title, _activeSectionId),
                SelectSectionCommand));
        }
    }

    private void RefreshActiveSectionRows()
    {
        var rows = _sections.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.Title, _activeSectionId))?.Rows ?? [];
        Replace(ActiveSectionRows, SortRows(rows));
    }

    private IEnumerable<DetailRowPresentation> SortRows(IEnumerable<DetailRowPresentation> rows)
    {
        return (_sortColumn.ToUpperInvariant(), _sortDescending) switch
        {
            ("NAME", true) => rows.OrderByDescending(x => x.Name),
            ("NAME", false) => rows.OrderBy(x => x.Name),
            ("META", true) => rows.OrderByDescending(x => x.MetaText),
            ("META", false) => rows.OrderBy(x => x.MetaText),
            ("SECONDARY", true) => rows.OrderByDescending(x => x.SecondaryText),
            ("SECONDARY", false) => rows.OrderBy(x => x.SecondaryText),
            (_, true) => rows.OrderByDescending(x => x.PrimaryText),
            _ => rows.OrderBy(x => x.PrimaryText)
        };
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
