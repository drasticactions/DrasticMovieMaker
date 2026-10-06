using Avalonia;
using Avalonia.VisualTree;
using AvaWpf;
using AvaMovieMaker.Dialogs;
using AvaMovieMaker.Settings;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.Services;

internal sealed class MessageBoxes(Visual owner, ISettingsStore store) : IMessageBoxes
{
    public async Task<MessageResult> ShowAsync(MessageRequest request)
    {
        if (request.DontShowAgainKey is { } key && store.Settings.DismissedWarnings.Contains(key))
        {
            return MessageResult.Ok;
        }

        MessageBoxWindow box = MessageBoxWindow.Create(request.Title, request);
        MessageResult r = owner.IsEffectivelyVisible && owner.IsAttachedToVisualTree() ? await WindowHost.ShowDialogAsync<MessageResult>(box, owner) : MessageResult.Ok;
        if (box.DontShowAgain && request.DontShowAgainKey is { } k)
        {
            store.Settings.DismissedWarnings.Add(k);
            store.Save();
        }

        return r;
    }
}
