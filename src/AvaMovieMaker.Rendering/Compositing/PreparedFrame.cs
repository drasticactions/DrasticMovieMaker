using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Rendering.Plan;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Rendering.Compositing;

public sealed class PreparedFrame : IFrameProvider
{
    private readonly Dictionary<(string Path, MediaTime Time), DecodedFrame?> _frames;
    private int _claimed;

    private PreparedFrame(FramePlan plan, int width, int height, IEffectLibrary effects, Dictionary<(string, MediaTime), DecodedFrame?> frames)
    {
        Plan = plan;
        Width = width;
        Height = height;
        Effects = effects;
        _frames = frames;
    }

    public FramePlan Plan { get; }

    public int Width { get; }

    public int Height { get; }

    public IEffectLibrary Effects { get; }

    public static PreparedFrame Prepare(FramePlan plan, int width, int height, IEffectLibrary effects, IFrameProvider source)
    {
        var frames = new Dictionary<(string, MediaTime), DecodedFrame?>();
        foreach (OverlayTitle title in plan.Titles)
        {
            effects.PrepareTitle(title.Content);
        }

        foreach (ClipInput? input in new[] { plan.A, plan.B })
        {
            if (input?.Source is TitleSource titleSource)
            {
                effects.PrepareTitle(titleSource.Content);
            }
            else if (input?.Source is MediaSource m)
            {
                MediaTime t = m.IsPicture ? MediaTime.Zero : m.SourceTime;
                if (!frames.ContainsKey((m.Path, t)))
                {
                    frames[(m.Path, t)] = source.GetFrame(m.Path, t);
                }
            }
        }

        return new PreparedFrame(plan, width, height, effects, frames);
    }

    public bool TryClaim() => Interlocked.Exchange(ref _claimed, 1) == 0;

    public DecodedFrame? GetFrame(string path, MediaTime time)
    {
        lock (_frames)
        {
            return _frames.Remove((path, time), out DecodedFrame? f) ? f : null;
        }
    }

    public void Release()
    {
        lock (_frames)
        {
            foreach (DecodedFrame? f in _frames.Values)
            {
                f?.Dispose();
            }

            _frames.Clear();
        }
    }
}
