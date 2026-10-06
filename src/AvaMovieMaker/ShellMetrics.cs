using Avalonia;

namespace AvaMovieMaker;

public static class ShellMetrics
{
    public const double MenuBarHeight = 25;
    public const double ToolBarHeight = 36;
    public const double BandsHeight = 61;
    public const double BandsLeftInset = 11;

    public const double UpperHeight = 459;
    public const double LowerHeight = 194;
    public const double SplitterSize = 4;

    public const double TasksWidth = 190;
    public const double TasksHeaderHeight = 26;

    public const double ContentsWidth = 450;
    public const double ContentsToolbarTop = 4;
    public const double ContentsToolbarHeight = 28;
    public const double ContentsListTop = 36;

    public const double MonitorWidth = 376;
    public const double PreviewLeft = 19;
    public const double PreviewTop = 71;
    public const double PreviewWidth = 338;
    public const double PreviewHeight = 254;
    public const double PlayerControlsHeight = 61;

    public const double LowerToolbarHeight = 25;
    public const double RulerHeight = 18;
    public const double TrackHeaderWidth = 114;
    public const double VideoTrackHeight = 55;
    public const double AudioMusicTrackHeight = 54;
    public const double TitleTrackHeight = 26;
    public const double ExpandedRowHeight = 21;
    public const double HorizontalScrollHeight = 17;

    public const double DefaultPixelsPerSecond = 16;

    public const double TaskSectionLeft = 14;
    public const double TaskLinkLeft = 28;
    public const double TaskLinkPitch = 19;
    public const double TaskFirstSectionTop = 12;

    public const double StoryboardCellWidth = 160;
    public const double StoryboardCellHeight = 136;
    public const double StoryboardSlotWidth = 78;

    public const double ContentsCellWidth = 134;
    public const double ContentsCellHeight = 140;
    public const double ThumbnailWidth = 91;
    public const double ThumbnailHeight = 68;

    public const double WindowWidth = 1024;
    public const double WindowHeight = 738;

    public static readonly Thickness MenuMargin = new(BandsLeftInset - 6, 1, 0, 0);
    public static readonly Thickness ToolBarMargin = new(BandsLeftInset - 2, 0, 0, 0);
}
