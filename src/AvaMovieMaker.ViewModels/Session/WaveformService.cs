using System.Collections.Concurrent;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Media.Analysis;

namespace AvaMovieMaker.ViewModels.Session;

public sealed class WaveformService
{
    private readonly ConcurrentDictionary<string, Task<Waveform?>> _cache = new(StringComparer.Ordinal);

    public event Action<string>? Ready;

    public Waveform? TryGet(string path)
    {
        Task<Waveform?> t = _cache.GetOrAdd(path, Build);
        return t.IsCompletedSuccessfully ? t.Result : null;
    }

    private Task<Waveform?> Build(string path)
    {
        Task<Waveform?> build = Task.Run(() =>
        {
            try
            {
                return (Waveform?)WaveformBuilder.Get(path);
            }
            catch (Exception e)
            {
                Log.Debug("waveform", $"{path}: {e.Message}");
                return null;
            }
        });

        build.ContinueWith(t =>
        {
            if (t.Result is not null)
            {
                Ready?.Invoke(path);
            }
        }, TaskScheduler.Default);
        return build;
    }
}
