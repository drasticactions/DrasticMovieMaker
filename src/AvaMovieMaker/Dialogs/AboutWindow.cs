using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaWpf;
using AvaMovieMaker.Controls;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.ViewModels;

namespace AvaMovieMaker.Dialogs;

public sealed class AboutWindow : UserControl
{
    public AboutWindow(string renderer)
    {
        WindowSettings.SetTitle(this, string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.AboutWindowTitle, Strings.AppName));
        WindowSettings.SetFrameKind(this, WindowFrameKind.Dialog);
        WindowSettings.SetCanResize(this, false);
        WindowSettings.SetWidth(this, 420);
        WindowSettings.SetSizeToContent(this, SizeToContent.Height);
        WindowSettings.SetWindowStartupLocation(this, WindowStartupLocation.CenterOwner);
        WindowSettings.SetShowInTaskbar(this, false);

        string version = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.0";
        var banner = new Border
        {
            Height = 74,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromRgb(0x1C, 0x4C, 0xA8), 0), new GradientStop(Color.FromRgb(0x2E, 0x8C, 0x9C), 1) },
            },
            Child = new TextBlock { Text = Strings.AppName, FontSize = 26, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center },
        };
        var stripe = new Border { Height = 4, Background = new SolidColorBrush(Color.FromRgb(0xF0, 0x9C, 0x1C)) };

        var text = new StackPanel { Spacing = 10 };
        text.Children.Add(new TextBlock { Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.AboutVersion, Strings.AppName, version), });
        text.Children.Add(new TextBlock { Text = Strings.AboutTitle, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = Strings.AboutLicense,
        });
        text.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)), Margin = new Thickness(0, 4) });
        text.Children.Add(new TextBlock { Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.AboutSystem, FFmpegRuntime.VersionText, renderer), Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)) });

        var icon = new Viewbox { Width = 32, Height = 32, Child = new Glyph { Kind = "app" }, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 16, 0) };
        var body = new DockPanel { Margin = new Thickness(16, 16, 16, 8) };
        DockPanel.SetDock(icon, Dock.Left);
        body.Children.Add(icon);
        body.Children.Add(text);

        var ok = new Button { Content = Strings.ButtonOk, MinWidth = 73, IsDefault = true, IsCancel = true };
        ok.Click += (_, _) => WindowHost.CloseDialog(this);
        var licenses = new Button { Content = Strings.ButtonLicenses, MinWidth = 73 };
        licenses.Click += async (_, _) => await WindowHost.ShowDialogAsync<object>(new LicensesWindow(), this);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 8, 16, 14), Children = { licenses, ok } };
        var root = new StackPanel();
        root.Children.Add(banner);
        root.Children.Add(stripe);
        root.Children.Add(body);
        root.Children.Add(buttons);
        Content = root;

        WindowHost.AddOpenedHandler(this, (_, _) => ok.Focus());
    }
}

public sealed class LicensesWindow : UserControl
{
    public LicensesWindow()
    {
        WindowSettings.SetTitle(this, Strings.LicensesWindowTitle);
        WindowSettings.SetFrameKind(this, WindowFrameKind.Dialog);
        WindowSettings.SetSizeToContent(this, SizeToContent.WidthAndHeight);
        WindowSettings.SetWindowStartupLocation(this, WindowStartupLocation.CenterOwner);
        WindowSettings.SetShowInTaskbar(this, false);
        string notices;
        using (Stream? s = typeof(LicensesWindow).Assembly.GetManifestResourceStream("THIRD-PARTY-NOTICES.md"))
        {
            notices = s is null ? string.Empty : new StreamReader(s).ReadToEnd();
        }

        var ok = new Button { Content = Strings.ButtonOk, MinWidth = 73, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => WindowHost.CloseDialog(this);
        var text = new TextBox
        {
            Text = notices,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Width = 640,
            Height = 420,
            [Avalonia.Automation.AutomationProperties.NameProperty] = Strings.LicensesWindowTitle,
        };
        Content = new StackPanel { Margin = new Thickness(12), Spacing = 10, Children = { text, ok } };
    }
}
