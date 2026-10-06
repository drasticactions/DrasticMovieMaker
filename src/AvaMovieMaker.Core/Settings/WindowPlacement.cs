namespace AvaMovieMaker.Settings;

public sealed class WindowPlacement
{
    public int X { get; set; } = -1;

    public int Y { get; set; } = -1;

    public int Width { get; set; }

    public int Height { get; set; }

    public bool Maximized { get; set; }

    public double TasksPaneWidth { get; set; }

    public double MonitorWidth { get; set; }

    public double LowerHeight { get; set; }
}
