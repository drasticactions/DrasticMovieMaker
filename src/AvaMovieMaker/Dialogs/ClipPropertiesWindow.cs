using AvaMovieMaker.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaWpf;
using AvaMovieMaker.Controls;
using AvaMovieMaker.ViewModels.Dialogs;

namespace AvaMovieMaker.Dialogs;

public sealed class ClipPropertiesWindow : UserControl
{
    public ClipPropertiesWindow(ClipPropertiesViewModel vm)
    {
        WindowSettings.SetTitle(this, string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ClipPropertiesWindowTitle, vm.Name));
        WindowSettings.SetFrameKind(this, WindowFrameKind.Dialog);
        WindowSettings.SetCanResize(this, false);
        WindowSettings.SetWidth(this, 425);
        WindowSettings.SetHeight(this, 520);
        WindowSettings.SetWindowStartupLocation(this, WindowStartupLocation.CenterOwner);
        WindowSettings.SetShowInTaskbar(this, false);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("123,*"), RowDefinitions = new RowDefinitions("84,19,*") };
        var icon = new Viewbox { Width = 48, Height = 48, Child = new Glyph { Kind = "import" }, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 0, 0) };
        grid.Children.Add(icon);
        var name = new TextBox { Text = vm.Name, IsReadOnly = true, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 28, 0, 0) };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);
        var rule = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(rule, 1);
        Grid.SetColumnSpan(rule, 2);
        grid.Children.Add(rule);

        var list = new Grid { ColumnDefinitions = new ColumnDefinitions("140,*") };
        var header = new Border { Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4)), BorderBrush = new SolidColorBrush(Color.FromRgb(0xD5, 0xD5, 0xD5)), BorderThickness = new Thickness(0, 0, 0, 1), Height = 24 };
        var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("140,*") };
        headerGrid.Children.Add(new TextBlock { Text = Strings.ColumnProperty, Margin = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center });
        var valueHeader = new TextBlock { Text = Strings.ColumnValue, Margin = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(valueHeader, 1);
        headerGrid.Children.Add(valueHeader);
        header.Child = headerGrid;
        int row = 0;
        foreach ((string label, string value) in vm.Rows)
        {
            list.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var l = new TextBlock { Text = label, Margin = new Thickness(6, 2) };
            var v = new SelectableTextBlock { Text = value, Margin = new Thickness(6, 2), TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(l, row);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            list.Children.Add(l);
            list.Children.Add(v);
            row++;
        }

        var listPanel = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        listPanel.Children.Add(header);
        listPanel.Children.Add(new ScrollViewer { Content = list });
        var listBorder = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0x82, 0x87, 0x90)), BorderThickness = new Thickness(1), Background = Brushes.White, Child = listPanel };
        Grid.SetRow(listBorder, 2);
        Grid.SetColumnSpan(listBorder, 2);
        grid.Children.Add(listBorder);

        var tabs = new TabControl { Padding = new Thickness(8), ItemsSource = new[] { new TabItem { Header = Strings.TabGeneral, Content = grid } } };
        var ok = new Button { Content = Strings.ButtonOk, MinWidth = 73, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        ok.Click += (_, _) => WindowHost.CloseDialog(this);
        var root = new DockPanel { Margin = new Thickness(12, 10, 12, 12) };
        DockPanel.SetDock(ok, Dock.Bottom);
        root.Children.Add(ok);
        root.Children.Add(tabs);
        Content = root;
    }
}
