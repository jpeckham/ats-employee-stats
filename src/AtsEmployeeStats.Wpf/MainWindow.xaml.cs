using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AtsEmployeeStats.Wpf.Controls;
using AtsEmployeeStats.Wpf.ViewModels;

namespace AtsEmployeeStats.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }

    private void Explorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            e.NewValue is ExplorerNodeViewModel node &&
            viewModel.SelectExplorerNodeCommand.CanExecute(node))
        {
            viewModel.SelectExplorerNodeCommand.Execute(node);
        }
    }

    private void DetailGrid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is DataGrid grid)
            ConfigureDetailGridColumns(grid);
    }

    private void DetailGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            ConfigureDetailGridColumns(grid);
    }

    private static void ConfigureDetailGridColumns(DataGrid grid)
    {
        if (grid.DataContext is not DetailTabViewModel tab)
            return;

        grid.Columns.Clear();
        grid.Columns.Add(CreateActionColumn());

        foreach (var column in tab.Columns)
        {
            grid.Columns.Add(column.IsTrend
                ? CreateSparklineColumn(column)
                : CreateTextColumn(column));
        }
    }

    private void DetailGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: GridRowViewModel row } &&
            DataContext is MainWindowViewModel viewModel &&
            viewModel.OpenRowCommand.CanExecute(row))
        {
            viewModel.OpenRowCommand.Execute(row);
        }
    }

    private static DataGridColumn CreateActionColumn()
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetValue(ContentControl.ContentProperty, "View");
        button.SetValue(FrameworkElement.MinWidthProperty, 52.0);
        button.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 1, 6, 1));
        button.SetBinding(
            Button.CommandProperty,
            new Binding("DataContext.OpenRowCommand")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Window), 1)
            });
        button.SetBinding(Button.CommandParameterProperty, new Binding("."));
        button.SetBinding(
            UIElement.IsEnabledProperty,
            new Binding(nameof(GridRowViewModel.Target))
            {
                Converter = NullToBooleanConverter.Instance
            });

        return new DataGridTemplateColumn
        {
            Header = string.Empty,
            Width = DataGridLength.Auto,
            CellTemplate = new DataTemplate { VisualTree = button }
        };
    }

    private static DataGridColumn CreateTextColumn(TableColumnViewModel column) =>
        new DataGridTextColumn
        {
            Header = column.Header,
            Binding = new Binding(column.BindingPath),
            SortMemberPath = column.SortMemberPath ?? column.BindingPath,
            Width = new DataGridLength(column.Width, DataGridLengthUnitType.Star)
        };

    private static DataGridColumn CreateSparklineColumn(TableColumnViewModel column)
    {
        var sparkline = new FrameworkElementFactory(typeof(SparklineControl));
        sparkline.SetValue(FrameworkElement.HeightProperty, 22.0);
        sparkline.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        sparkline.SetBinding(SparklineControl.ValuesProperty, new Binding(column.BindingPath));

        return new DataGridTemplateColumn
        {
            Header = column.Header,
            SortMemberPath = column.SortMemberPath ?? column.BindingPath,
            Width = new DataGridLength(column.Width, DataGridLengthUnitType.Star),
            CellTemplate = new DataTemplate { VisualTree = sparkline }
        };
    }
}

internal sealed class NullToBooleanConverter : IValueConverter
{
    public static readonly NullToBooleanConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
