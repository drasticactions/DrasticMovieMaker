using System.Collections.Concurrent;
using AvaMovieMaker.Audio.Native;
using AvaMovieMaker.Media.Encoders;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Audio.Capture;

public sealed class NarrationRecorder : INarrationRecording
{
    public const int SampleRate = 44100;
    public const int Channels = 2;

    private readonly BlockingCollection<float[]> _queue = new();
    private readonly NativeDevice _device;
    private readonly AudioFileWriter _writer;
    private readonly Thread _thread;
    private long _frames;
    private float _peak;
    private bool _stopped;

    private NarrationRecorder(NativeDevice? device, string path, MediaTime? maxDuration)
    {
        _writer = new AudioFileWriter(path, Channels, SampleRate);
        Path = path;
        MaxDuration = maxDuration;
        _thread = new Thread(WriteLoop) { IsBackground = true, Name = "AvaMovieMaker narration" };
        _device = device!;
    }

    public string Path { get; }

    public MediaTime? MaxDuration { get; }

    public MediaTime Duration => MediaTime.FromTimeBase(Interlocked.Read(ref _frames), new Rational(1, SampleRate));

    public float Gain { get; set; } = 1f;

    public float Level => Interlocked.Exchange(ref _peak, 0f);

    public bool IsRecording => !_stopped;

    public event EventHandler? LimitReached;

    public static NarrationRecorder? Start(string deviceName, string path, MediaTime? maxDuration = null)
    {
        if (!MiniAudioContext.TryGet(out _))
        {
            return null;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        NarrationRecorder? rec = null;
        NativeDevice? dev = NativeDevice.OpenCapture(AudioSystem.IndexOf(deviceName, capture: true), SampleRate, Channels,
            input => rec?.OnInput(input));
        if (dev is null)
        {
            return null;
        }

        rec = new NarrationRecorder(dev, path, maxDuration);
        rec._thread.Start();
        dev.Start();
        return rec;
    }

    private void OnInput(ReadOnlySpan<float> input)
    {
        if (_stopped)
        {
            return;
        }

        float gain = Gain;
        float[] block = input.ToArray();
        float peak = 0;
        for (int i = 0; i < block.Length; i++)
        {
            block[i] *= gain;
            peak = Math.Max(peak, Math.Abs(block[i]));
        }

        float old = Volatile.Read(ref _peak);
        if (peak > old)
        {
            Interlocked.Exchange(ref _peak, peak);
        }

        _queue.Add(block);
    }

    private void WriteLoop()
    {
        long limit = MaxDuration is { } m ? m.ToTimeBase(new Rational(1, SampleRate)) : long.MaxValue;
        foreach (float[] block in _queue.GetConsumingEnumerable())
        {
            long have = Interlocked.Read(ref _frames);
            int take = (int)Math.Min(block.Length / Channels, Math.Max(0, limit - have));
            if (take > 0)
            {
                _writer.Write(block.AsSpan(0, take * Channels));
                Interlocked.Add(ref _frames, take);
            }

            if (have + take >= limit && !_stopped)
            {
                _stopped = true;
                LimitReached?.Invoke(this, EventArgs.Empty);
            }
        }

        _writer.Finish();
    }

    public MediaTime Stop()
    {
        _stopped = true;
        _device.Stop();
        _queue.CompleteAdding();
        _thread.Join();
        return Duration;
    }

    public void Dispose()
    {
        if (!_queue.IsAddingCompleted)
        {
            Stop();
        }

        _device.Dispose();
    }
}
