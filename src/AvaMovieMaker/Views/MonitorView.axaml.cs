using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaMovieMaker.Controls;
using AvaMovieMaker.ViewModels.Preview;

namespace AvaMovieMaker.Views;

public partial class MonitorView : UserControl
{
    public MonitorView()
    {
        AvaloniaXamlLoader.Load(this);
        PreviewImage preview = this.FindControl<PreviewImage>("Preview")!;
        preview.PixelSizeChanged += (w, h) => (DataContext as MonitorViewModel)?.Resize(w, h);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MonitorViewModel vm)
            {
                vm.Surface = preview;
            }
        };
    }
}
