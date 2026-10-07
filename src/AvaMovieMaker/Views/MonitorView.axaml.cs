using Avalonia;
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
        TextBlock caption = this.FindControl<TextBlock>("Caption")!;
        preview.PixelSizeChanged += (w, h) => (DataContext as MonitorViewModel)?.Resize(w, h);
        preview.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty || e.Property == PreviewImage.AspectProperty)
            {
                Rect video = preview.VideoRect;
                caption.MaxWidth = video.Width < video.Height ? preview.Bounds.Width : video.Width;
            }
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MonitorViewModel vm)
            {
                vm.Surface = preview;
            }
        };
    }
}
