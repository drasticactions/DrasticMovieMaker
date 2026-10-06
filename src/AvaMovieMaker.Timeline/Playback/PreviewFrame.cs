using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Playback;

public sealed class PreviewFrame
{
    public PreviewFrame(byte[] pixels, int width, int height, MediaTime time)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
        Time = time;
    }

    public PreviewFrame(SharedFrameBuffer shared, MediaTime time)
    {
        Pixels = [];
        Shared = shared;
        Width = shared.Width;
        Height = shared.Height;
        Time = time;
    }

    public PreviewFrame(PreparedFrame prepared, MediaTime time)
    {
        Pixels = [];
        Prepared = prepared;
        Width = prepared.Width;
        Height = prepared.Height;
        Time = time;
    }

    public byte[] Pixels { get; }

    public PreparedFrame? Prepared { get; }

    public SharedFrameBuffer? Shared { get; }

    public int Width { get; }

    public int Height { get; }

    public MediaTime Time { get; }

    public void Drop()
    {
        Shared?.Release(consumed: false);
        if (Prepared is { } p && p.TryClaim())
        {
            p.Release();
        }
    }
}
