namespace AtsEmployeeStats.Maui.Views;

using Microsoft.Maui.Layouts;

internal static class DetailPageLayout
{
    public static View Create(Func<string, Task> navigateAsync)
    {
        var title = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold
        };
        SetThemeText(title, "#20242A", "#F2F4F7");
        title.SetBinding(Label.TextProperty, "Title");

        var subtitle = new Label { FontSize = 13 };
        SetThemeText(subtitle, "#64707D", "#AAB2BD");
        subtitle.SetBinding(Label.TextProperty, "Subtitle");

        var status = new Label { FontSize = 13 };
        SetThemeText(status, "#4F5B67", "#B5BDC7");
        status.SetBinding(Label.TextProperty, "StatusText");

        var spinner = new ActivityIndicator();
        spinner.SetBinding(ActivityIndicator.IsRunningProperty, "IsBusy");
        spinner.SetBinding(VisualElement.IsVisibleProperty, "IsBusy");

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 16
        };
        header.Add(new VerticalStackLayout
        {
            Spacing = 3,
            Children = { title, subtitle, status }
        });
        header.Add(spinner, 1);

        var metrics = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            Direction = FlexDirection.Row,
            AlignItems = FlexAlignItems.Stretch
        };
        metrics.SetBinding(BindableLayout.ItemsSourceProperty, "Metrics");
        BindableLayout.SetItemTemplate(metrics, new DataTemplate(CreateMetricView));

        var sectionTabs = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            Direction = FlexDirection.Row,
            AlignItems = FlexAlignItems.Center
        };
        sectionTabs.SetBinding(BindableLayout.ItemsSourceProperty, "SectionTabs");
        BindableLayout.SetItemTemplate(sectionTabs, new DataTemplate(CreateSectionTabView));

        var sortHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        sortHeader.Add(CreateSortButton("Name", "Name"));
        sortHeader.Add(CreateSortButton("Value", "Primary"), 1);
        sortHeader.Add(CreateSortButton("Trend", "Secondary"), 2);
        sortHeader.Add(CreateSortButton("Meta", "Meta"), 3);

        var activeRows = new VerticalStackLayout { Spacing = 10 };
        activeRows.SetBinding(BindableLayout.ItemsSourceProperty, "ActiveSectionRows");
        BindableLayout.SetItemTemplate(activeRows, new DataTemplate(() => CreateRowView(navigateAsync)));

        var relatedData = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children = { sectionTabs, sortHeader, activeRows }
            }
        };
        SetCardTheme(relatedData);

        var emptyState = CreateEmptyState();

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(24, 22),
            Spacing = 16,
            MaximumWidthRequest = 1360,
            HorizontalOptions = LayoutOptions.Center,
            Children = { header, emptyState, metrics, relatedData }
        };

        SetThemeBackground(content, "#F6F7F9", "#15171A");

        return new ScrollView
        {
            Content = content
        };
    }

    private static object CreateMetricView()
    {
        var value = new Label { FontSize = 18, FontAttributes = FontAttributes.Bold };
        value.SetBinding(Label.TextProperty, "Value");

        var label = new Label { FontSize = 12 };
        SetThemeText(label, "#6A737D", "#AAB2BD");
        label.SetBinding(Label.TextProperty, "Label");

        var border = new Border
        {
            Padding = 14,
            Margin = new Thickness(0, 0, 10, 10),
            MinimumWidthRequest = 132,
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children = { value, label }
            }
        };
        SetCardTheme(border);
        return border;
    }

    private static object CreateSectionView(Func<string, Task> navigateAsync)
    {
        var title = new Label { FontSize = 18, FontAttributes = FontAttributes.Bold };
        title.SetBinding(Label.TextProperty, "Title");

        var rows = new VerticalStackLayout { Spacing = 8 };
        rows.SetBinding(BindableLayout.ItemsSourceProperty, "Rows");
        BindableLayout.SetItemTemplate(rows, new DataTemplate(() => CreateRowView(navigateAsync)));

        var border = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { title, rows }
            }
        };
        SetCardTheme(border);
        return border;
    }

    private static object CreateSectionTabView()
    {
        var button = new Button
        {
            FontSize = 13,
            Padding = new Thickness(14, 7),
            Margin = new Thickness(0, 0, 8, 8),
            HeightRequest = 34,
            MinimumWidthRequest = 92
        };
        button.SetBinding(Button.TextProperty, "Title");
        button.SetBinding(Button.CommandProperty, "SelectCommand");
        button.SetBinding(Button.CommandParameterProperty, "Id");
        return button;
    }

    private static Button CreateSortButton(string text, string parameter)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(10, 4),
            HeightRequest = 30,
            MinimumWidthRequest = 64
        };
        button.SetBinding(Button.CommandProperty, "SortSectionRowsCommand");
        button.CommandParameter = parameter;
        return button;
    }

    private static object CreateRowView(Func<string, Task> navigateAsync)
    {
        var name = new Button
        {
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(0),
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Colors.Transparent
        };
        name.SetBinding(Button.TextProperty, "Name");
        name.Clicked += async (_, _) =>
        {
            if (name.BindingContext is Presentation.DetailRowPresentation rowModel &&
                !string.IsNullOrWhiteSpace(rowModel.ActionRoute))
            {
                await navigateAsync(rowModel.ActionRoute);
            }
        };

        var primary = new Label();
        primary.SetBinding(Label.TextProperty, "PrimaryText");

        var meta = new Label();
        SetThemeText(meta, "#6A737D", "#AAB2BD");
        meta.SetBinding(Label.TextProperty, "MetaText");

        var secondary = new Label { FontSize = 12 };
        SetThemeText(secondary, "#6A737D", "#AAB2BD");
        secondary.SetBinding(Label.TextProperty, "SecondaryText");

        var sparkline = new Label { FontSize = 12, HorizontalTextAlignment = TextAlignment.End };
        SetThemeText(sparkline, "#427A61", "#8BD2A5");
        sparkline.SetBinding(Label.TextProperty, "SparklineText");

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.3, GridUnitType.Star)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        row.Add(name);
        row.Add(primary, 1);
        row.Add(sparkline, 2);
        row.Add(meta, 3);
        row.Add(secondary, 0, 1);
        Grid.SetColumnSpan(secondary, 4);
        return row;
    }

    private static View CreateEmptyState()
    {
        var title = new Label
        {
            Text = "No detail loaded",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };
        SetThemeText(title, "#20242A", "#F2F4F7");

        var message = new Label
        {
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        SetThemeText(message, "#5B6470", "#B5BDC7");
        message.SetBinding(Label.TextProperty, "StatusText");

        var emptyState = new Border
        {
            Padding = new Thickness(28, 24),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children = { title, message }
            }
        };
        emptyState.SetBinding(VisualElement.IsVisibleProperty, "HasNoContent");
        SetCardTheme(emptyState);
        return emptyState;
    }

    private static void SetCardTheme(Border border)
    {
        border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#D9DEE6"), Color.FromArgb("#303640"));
        border.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#FFFFFF"), Color.FromArgb("#20242A"));
    }

    private static void SetThemeText(Label label, string light, string dark) =>
        label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb(light), Color.FromArgb(dark));

    private static void SetThemeBackground(VisualElement element, string light, string dark) =>
        element.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb(light), Color.FromArgb(dark));
}
