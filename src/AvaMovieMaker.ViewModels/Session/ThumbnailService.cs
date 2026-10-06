using System.Collections.Concurrent;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Media.Analysis;
using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Time;
using SkiaSharp;

namespace AvaMovieMaker.ViewModels.Session;

public sealed class ThumbnailService(DecoderPool pool)
{
    private const int MemoryLimit = 512;
    private readonly ConcurrentDictionary<string, Task<SKBitmap?>> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _slots = new(2, 2);

    public Task<SKBitmap?> GetAsync(string path, MediaTime time, int width, int height)
    {
        string key = $"{path}|{time.Ticks}|{width}x{height}";
        if (_cache.Count > MemoryLimit)
        {
            _cache.Clear();
        }

        return _cache.GetOrAdd(key, _ => LoadAsync(path, time, width, height));
    }

    private async Task<SKBitmap?> LoadAsync(string path, MediaTime time, int width, int height)
    {
        await _slots.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Thumbnailer.Get(path, time, width, height, pool)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Debug("thumbs", $"{path}: {e.Message}");
            return null;
        }
        finally
        {
            _slots.Release();
        }
    }

    public static SKBitmap TitleThumbnail(TitleContent content, int width, int height)
    {
        var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        double d = Effects.Catalog.TitleAnimationCatalog.DefaultDuration(content);
        TitleAnimator.Draw(canvas, width, height, content, d / 2, d, null, fullFrame: content.IsFullFrame);
        return bmp;
    }

    public void Forget(string path)
    {
        foreach (string k in _cache.Keys.Where(k => k.StartsWith(path + "|", StringComparison.Ordinal)).ToList())
        {
            _cache.TryRemove(k, out _);
        }
    }
}
