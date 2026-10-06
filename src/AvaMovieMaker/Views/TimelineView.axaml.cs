using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using AvaMovieMaker.Controls;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.Views;

public partial class TimelineView : UserControl
{
    private readonly TimelineControl _control;
    private readonly ScrollBar _scroll;
    private bool _syncing;

    public TimelineView()
    {
        AvaloniaXamlLoader.Load(this);
        _control = this.FindControl<TimelineControl>("Control")!;
        _scroll = this.FindControl<ScrollBar>("HScroll")!;
        _scroll.Scroll += (_, _) =>
        {
            if (!_syncing && _control.DataContext is TimelineViewModel vm)
            {
                vm.ScrollSeconds = _scroll.Value;
            }
        };
        _control.LayoutChanged += SyncScroll;
    }

    private void SyncScroll()
    {
        if (_control.DataContext is not TimelineViewModel vm)
        {
            return;
        }

        _syncing = true;
        double visible = Math.Max(1, (_control.Bounds.Width - TimelineControl.HeaderWidth) / vm.PixelsPerSecond);
        double total = Math.Max(vm.Project.Duration.Seconds + visible * 0.5, visible);
        _scroll.Maximum = Math.Max(0, total - visible);
        _scroll.ViewportSize = visible;
        _scroll.SmallChange = visible / 20;
        _scroll.LargeChange = visible * 0.9;
        _scroll.Value = Math.Clamp(vm.ScrollSeconds, 0, _scroll.Maximum);
        _syncing = false;
    }

    public void ZoomToFit()
    {
        if (_control.DataContext is TimelineViewModel vm)
        {
            vm.ZoomToFit(Math.Max(100, _control.Bounds.Width - TimelineControl.HeaderWidth - 20));
        }
    }
}
