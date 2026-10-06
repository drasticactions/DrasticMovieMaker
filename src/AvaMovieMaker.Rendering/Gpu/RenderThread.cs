using System.Collections.Concurrent;

namespace AvaMovieMaker.Rendering.Gpu;

public sealed class RenderThread : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public RenderThread(string name = "AvaMovieMaker render")
    {
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.Start();
    }

    public bool IsCurrent => Thread.CurrentThread == _thread;

    private void Run()
    {
        foreach (Action a in _queue.GetConsumingEnumerable())
        {
            if (OperatingSystem.IsMacOS())
            {
                MetalDevice.InPool(a);
            }
            else
            {
                a();
            }
        }
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (IsCurrent)
        {
            try
            {
                return Task.FromResult(func());
            }
            catch (Exception e)
            {
                return Task.FromException<T>(e);
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });
        return tcs.Task;
    }

    public Task InvokeAsync(Action action) => InvokeAsync(() =>
    {
        action();
        return true;
    });

    public T Invoke<T>(Func<T> func) => IsCurrent ? func() : InvokeAsync(func).GetAwaiter().GetResult();

    public void Invoke(Action action) => Invoke(() =>
    {
        action();
        return true;
    });

    public void Dispose()
    {
        _queue.CompleteAdding();
        if (!IsCurrent)
        {
            _thread.Join(TimeSpan.FromSeconds(5));
        }
    }
}
