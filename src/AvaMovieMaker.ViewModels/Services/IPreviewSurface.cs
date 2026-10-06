using AvaMovieMaker.Timeline.Playback;

namespace AvaMovieMaker.ViewModels.Services;

public interface IPreviewSurface
{
    void Present(PreviewFrame frame);

    byte[]? SharedFrameDevice { get; }

    event EventHandler? SharedFrameDeviceChanged;

    bool ComposesFrames { get; }

    event EventHandler? ComposesFramesChanged;

    void SharedFramesRefused()
    {
    }
}
