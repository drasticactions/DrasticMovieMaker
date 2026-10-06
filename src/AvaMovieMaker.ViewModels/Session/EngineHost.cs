using AvaMovieMaker.Effects;
using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Timeline.Playback;

namespace AvaMovieMaker.ViewModels.Session;

public sealed class EngineHost : IDisposable
{
    public EngineHost(bool preferGpu = true, bool hardwareDecode = true, string audioDevice = "", bool withAudio = true)
    {
        Device = RenderDevice.Create(preferGpu);
        Decoders = new DecoderPool(8, hardwareDecode);
        Frames = new PooledFrameProvider(Decoders) { KeepHardwareFrames = hardwareDecode && Device.SupportsZeroCopy };
        Playback = new PlaybackEngine(Device, EffectLibrary.Instance, Frames, withAudio ? audioDevice : null);
        Thumbnails = new ThumbnailService(Decoders);
    }

    public RenderDevice Device { get; }

    public DecoderPool Decoders { get; }

    public PooledFrameProvider Frames { get; }

    public PlaybackEngine Playback { get; }

    public ThumbnailService Thumbnails { get; }

    public void Dispose()
    {
        Playback.Dispose();
        Decoders.Dispose();
        Device.Dispose();
    }
}
