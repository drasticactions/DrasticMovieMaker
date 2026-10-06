using Avalonia.Threading;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.Services;

internal sealed class UiDispatcher : AvaMovieMaker.ViewModels.Services.IDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
