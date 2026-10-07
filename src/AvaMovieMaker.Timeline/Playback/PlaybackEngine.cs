using AvaMovieMaker.Audio;
using AvaMovieMaker.Audio.Mixing;
using AvaMovieMaker.Audio.Playback;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Planning;
using SkiaSharp;

namespace AvaMovieMaker.Timeline.Playback;

public sealed class PlaybackEngine : IDisposable
{
    private readonly Compositor _compositor;
    private readonly IEffectLibrary _effects;
    private readonly IFrameProvider _frames;
    private readonly AudioOutput? _output;
    private readonly StopwatchClock _stopwatch = new();
    private readonly AudioMixer _mixer = new(AudioPlan.Empty);
    private readonly Lock _gate = new();
    private RenderPlan _plan = RenderPlan.Empty;
    private Thread? _presenter;
    private static readonly MediaTime PrerollAhead = MediaTime.FromSeconds(1);
    private static readonly MediaTime FrameTolerance = MediaTime.FromMilliseconds(0.1);
    private int _presentGeneration;
    private MediaTime _position;
    private long _lastFrame = -1;
    private int _previewWidth = 320;
    private int _previewHeight = 240;
    private readonly System.Collections.Concurrent.ConcurrentQueue<(MediaTime Time, int Generation, TaskCompletionSource Done)> _requests = new();
    private int _rendering;
    private readonly SemaphoreSlim _sharedGate = new(1, 1);
    private SharedFramePool? _shared;
    private byte[]? _sharedUuid;

    public PlaybackEngine(RenderDevice device, IEffectLibrary effects, IFrameProvider frames, string? audioDevice = "")
    {
        Device = device;
        _compositor = new Compositor(device, effects, frames);
        _effects = effects;
        _frames = frames;
        _output = audioDevice is null ? null : AudioOutput.Open(audioDevice);
        if (_output is null)
        {
            Log.Info("playback", "No audio device; playback uses a stopwatch clock.");
        }
    }

    public RenderDevice Device { get; }

    public Compositor Compositor => _compositor;

    public bool HasAudioDevice => _output is not null;

    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public event Action<PreviewFrame>? FrameReady;

    public event EventHandler? StateChanged;

    public event Action<MediaTime>? PositionChanged;

    public RenderPlan Plan
    {
        get => _plan;
        set
        {
            _plan = value;
            _mixer.Plan = value.Audio;
            _lastFrame = -1;
            if (State != PlaybackState.Playing)
            {
                _ = RenderAsync(Position);
            }
        }
    }

    public MediaTime Duration => _plan.Duration;

    public MediaTime Position
    {
        get
        {
            if (State == PlaybackState.Playing)
            {
                MediaTime clock = _output is not null ? _output.Position : _stopwatch.Position;
                return MediaTime.Min(clock, Duration);
            }

            return _position;
        }
    }

    public float Volume
    {
        get => _output?.Volume ?? 1f;
        set
        {
            if (_output is not null)
            {
                _output.Volume = value;
            }
        }
    }

    public bool UsesSharedFrames => _shared is not null;

    public async Task UseSharedFramesAsync(byte[]? compositorDeviceUuid)
    {
        await _sharedGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_sharedUuid is not null && compositorDeviceUuid is not null && _sharedUuid.AsSpan().SequenceEqual(compositorDeviceUuid))
            {
                return;
            }

            SharedFramePool? old = _shared;
            _shared = null;
            _sharedUuid = null;
            old?.Dispose();
            if (compositorDeviceUuid is not null && Device.IsGpu)
            {
                _shared = await Task.Run(() => SharedFramePool.TryCreate(Device, compositorDeviceUuid)).ConfigureAwait(false);
                _sharedUuid = _shared is null ? null : compositorDeviceUuid;
            }
        }
        finally
        {
            _sharedGate.Release();
        }

        _lastFrame = -1;
        if (State != PlaybackState.Playing)
        {
            await RenderAsync(Position).ConfigureAwait(false);
        }
    }

    public bool UiComposes
    {
        get => _uiComposes;
        set
        {
            if (_uiComposes == value)
            {
                return;
            }

            _uiComposes = value;
            Log.Info("playback", value ? "Preview frames are composed on the interface's GPU" : "Preview frames are composed here");
            _lastFrame = -1;
            if (State != PlaybackState.Playing)
            {
                _ = RenderAsync(Position);
            }
        }
    }

    private volatile bool _uiComposes;

    public (int Width, int Height) PreviewSize => (_previewWidth, _previewHeight);

    public void SetPreviewSize(int width, int height)
    {
        _previewWidth = Math.Max(16, width);
        _previewHeight = Math.Max(16, height);
        _lastFrame = -1;
        if (State != PlaybackState.Playing)
        {
            _ = RenderAsync(Position);
        }
    }

    public void Play()
    {
        lock (_gate)
        {
            if (State == PlaybackState.Playing)
            {
                return;
            }

            if (_position >= Duration)
            {
                _position = MediaTime.Zero;
            }

            if (_output is not null)
            {
                _output.Play(_mixer, _position, Duration);
            }
            else
            {
                _stopwatch.Start(_position);
            }

            State = PlaybackState.Playing;
            int generation = ++_presentGeneration;
            _presenter = new Thread(() => Present(generation)) { IsBackground = true, Name = "AvaMovieMaker presenter" };
            _presenter.Start();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause() => Halt(PlaybackState.Paused);

    public void Stop()
    {
        Halt(PlaybackState.Stopped);
        Seek(MediaTime.Zero);
    }

    public void TogglePlayPause()
    {
        if (State == PlaybackState.Playing)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    private void Halt(PlaybackState next, bool wait = false)
    {
        Thread? presenter;
        lock (_gate)
        {
            if (State == PlaybackState.Playing)
            {
                _position = Position;
            }

            _presentGeneration++;
            presenter = _presenter;
            _presenter = null;
            State = next;
        }

        if (wait && presenter is not null && presenter != Thread.CurrentThread)
        {
            presenter.Join();
        }

        _output?.Stop();
        _stopwatch.Stop();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HaltAtEnd(int generation)
    {
        lock (_gate)
        {
            if (_presentGeneration != generation)
            {
                return;
            }
        }

        Halt(PlaybackState.Stopped);
    }

    public void Seek(MediaTime time)
    {
        time = time.Clamp(MediaTime.Zero, Duration);
        bool wasPlaying = State == PlaybackState.Playing;
        if (wasPlaying)
        {
            Halt(PlaybackState.Paused);
        }

        _position = time;
        _lastFrame = -1;
        if (wasPlaying)
        {
            Play();
        }
        else
        {
            _ = RenderAsync(time);
            PositionChanged?.Invoke(time);
        }
    }

    public void StepFrame(int frames)
    {
        if (State == PlaybackState.Playing)
        {
            Pause();
        }

        Rational rate = _plan.FrameRate;
        long index = Position.ToFrameNearest(rate) + frames;
        Seek(MediaTime.FromFrame(Math.Max(0, index), rate));
    }

    public void Scrub(MediaTime time)
    {
        Seek(time);
        if (_output is not null && State != PlaybackState.Playing)
        {
            _output.PlayGrain(_mixer, time, MediaTime.FromMilliseconds(40));
        }
    }

    private void Present(int generation)
    {
        Rational rate = _plan.FrameRate;
        MediaTime frameDur = MediaTime.FrameDuration(rate);
        while (Volatile.Read(ref _presentGeneration) == generation)
        {
            MediaTime t = Position;
            if (t >= Duration)
            {
                lock (_gate)
                {
                    if (_presentGeneration != generation)
                    {
                        return;
                    }

                    _position = Duration;
                }

                ThreadPool.QueueUserWorkItem(_ => HaltAtEnd(generation));
                PositionChanged?.Invoke(Duration);
                return;
            }

            long index = FrameAt(t, rate);
            if (index != _lastFrame)
            {
                _lastFrame = index;
                Preroll(MediaTime.FromFrame(index, rate));
                RenderAsync(MediaTime.FromFrame(index, rate), generation).Wait();
                if (Volatile.Read(ref _presentGeneration) != generation)
                {
                    return;
                }

                PositionChanged?.Invoke(t);
            }

            MediaTime next = MediaTime.FromFrame(index + 1, rate);
            int ms = (int)Math.Clamp((next - Position).Seconds * 1000, 1, frameDur.Seconds * 1000);
            Thread.Sleep(ms);
        }
    }

    private static long FrameAt(MediaTime time, Rational rate) => (time + FrameTolerance).ToFrameFloor(rate);

    private void Preroll(MediaTime from)
    {
        MediaTime until = from + PrerollAhead;
        foreach (VideoSpan span in _plan.Video)
        {
            if (span.Path is { } path && !span.IsPicture && span.Start > from && span.Start <= until)
            {
                _frames.Preroll(path, span.SourceIn);
            }
        }
    }

    public Task RenderAsync(MediaTime time)
    {
        Rational rate = _plan.FrameRate;
        time = MediaTime.FromFrame(FrameAt(time, rate), rate);
        Preroll(time);
        return RenderAsync(time, -1);
    }

    private Task RenderAsync(MediaTime time, int generation)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Enqueue((time, generation, done));
        if (Interlocked.CompareExchange(ref _rendering, 1, 0) == 0)
        {
            _ = Task.Run(RenderLoopAsync);
        }

        return done.Task;
    }

    private async Task RenderLoopAsync()
    {
        var taken = new List<TaskCompletionSource>();
        while (true)
        {
            MediaTime? latest = null;
            taken.Clear();
            while (_requests.TryDequeue(out var request))
            {
                if (request.Generation < 0 || request.Generation == Volatile.Read(ref _presentGeneration))
                {
                    latest = request.Time;
                }

                taken.Add(request.Done);
            }

            if (taken.Count == 0)
            {
                Volatile.Write(ref _rendering, 0);
                if (_requests.IsEmpty || Interlocked.CompareExchange(ref _rendering, 1, 0) != 0)
                {
                    return;
                }

                continue;
            }

            if (latest is not { } time)
            {
                taken.ForEach(d => d.TrySetResult());
                continue;
            }

            try
            {
                RenderPlan plan = _plan;
                int w = _previewWidth, h = _previewHeight;
                SharedFramePool? shared = _shared;
                bool prepare = _uiComposes;
                PreviewFrame frame = await Device.Thread.InvokeAsync(() =>
                {
                    if (prepare)
                    {
                        return new PreviewFrame(PreparedFrame.Prepare(plan.PlanAt(time), w, h, _effects, _frames), time);
                    }

                    using SKImage img = _compositor.Render(plan.PlanAt(time), w, h);
                    if (shared?.TryPresent(img) is { } buffer)
                    {
                        return new PreviewFrame(buffer, time);
                    }

                    byte[] px = new byte[w * h * 4];
                    Compositor.ReadPixels(img, px, bgra: true);
                    return new PreviewFrame(px, w, h, time);
                }).ConfigureAwait(false);
                if (FrameReady is { } ready)
                {
                    ready(frame);
                }
                else
                {
                    frame.Drop();
                }
            }
            catch (Exception e)
            {
                Log.Error("playback", $"Render at {time} failed", e);
            }

            taken.ForEach(d => d.TrySetResult());
        }
    }

    public async Task SaveFrameAsync(MediaTime time, int width, int height, string path)
    {
        string ext = Path.GetExtension(path);
        bool jpeg = ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpe", StringComparison.OrdinalIgnoreCase);
        byte[] data = await EncodeFrameAsync(time, width, height, jpeg);
        FileStore.WriteAllBytes(path, data);
    }

    public Task<byte[]> EncodeFrameAsync(MediaTime time, int width, int height, bool jpeg)
    {
        RenderPlan plan = _plan;
        return Device.Thread.InvokeAsync(() =>
        {
            using SKImage img = _compositor.Render(plan.PlanAt(time), width, height);
            byte[] px = new byte[width * height * 4];
            Compositor.ReadPixels(img, px, bgra: false);
            using var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            System.Runtime.InteropServices.Marshal.Copy(px, 0, bmp.GetPixels(), px.Length);
            using SKData data = bmp.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 95);
            return data.ToArray();
        });
    }

    public void Dispose()
    {
        Halt(PlaybackState.Stopped, wait: true);
        _output?.Dispose();
        _mixer.Dispose();
        _shared?.Dispose();
        _shared = null;
        Device.Thread.Invoke(_compositor.Dispose);
    }
}
