using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaMovieMaker.ViewModels;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.Views;

public enum BoardTarget
{
    Empty,

    VideoClip,

    Title,

    EffectStar,

    Transition,

    Audio,

    AudioMusic,

    TitleOverlay,
}

public static class BoardMenus
{
    internal static ContextMenu? Current { get; private set; }

    public static bool Show(Control owner, BoardTarget target)
    {
        if (For(owner, target) is not { } menu)
        {
            return false;
        }

        menu.Closed += (_, _) => Current = null;
        Current = menu;
        menu.Open(owner);
        return true;
    }

    public static ContextMenu? For(Control owner, BoardTarget target)
    {
        if (owner.FindAncestorOfType<ShellView>()?.DataContext is not ShellViewModel s)
        {
            return null;
        }

        MenuItem PlayBoard() => Item(s.IsTimeline ? Strings.PlayTimeline : Strings.PlayStoryboard, Strings.PromptPlayTimeline, s.Monitor.PlayProjectCommand, "Ctrl+W");
        MenuItem SelectAll() => Item(Strings.SelectAll, Strings.PromptSelectAll, s.SelectAllCommand, "Ctrl+A");
        MenuItem Effects() => Item(Strings.ContextEffects, Strings.PromptVideoEffects, s.VideoEffectsCommand);
        MenuItem FadeIn() => Item(Strings.FadeIn, Strings.PromptVideoFadeIn, s.VideoFadeInCommand);
        MenuItem FadeOut() => Item(Strings.FadeOut, Strings.PromptVideoFadeOut, s.VideoFadeOutCommand);
        MenuItem BrowseMissing() => Item(Strings.BrowseForMissingFile, Strings.PromptBrowseForMissingFile, s.BrowseMissingCommand);
        MenuItem Properties() => Item(Strings.ContextProperties, Strings.PromptProperties, s.ClipPropertiesCommand);
        object[] items = target switch
        {
            BoardTarget.VideoClip =>
            [
                .. Clipboard(s), "-",
                PlayBoard(), SelectAll(), "-",
                Effects(), FadeIn(), FadeOut(), "-",
                BrowseMissing(), Properties(),
            ],
            BoardTarget.Title =>
            [
                Item(Strings.EditTitle, Strings.PromptEditTitle, s.EditTitleCommand), "-",
                .. Clipboard(s), "-",
                PlayBoard(), SelectAll(), "-",
                Effects(), FadeIn(), FadeOut(),
            ],
            BoardTarget.EffectStar =>
            [
                Effects(), "-",
                Cut(s), Copy(s), Paste(s),
                Item(Strings.ContextRemoveEffect, null, s.RemoveEffectsCommand, "Delete"),
            ],
            BoardTarget.Transition =>
            [
                PlayBoard(), "-",
                Cut(s), Copy(s), Paste(s),
                Item(Strings.ContextRemoveTransition, null, s.RemoveCommand, "Delete"),
            ],
            BoardTarget.Audio =>
            [
                .. Clipboard(s), "-",
                SelectAll(), PlayBoard(), "-",
                .. AudioItems(s), "-",
                BrowseMissing(), Properties(),
            ],
            BoardTarget.AudioMusic =>
            [
                .. Clipboard(s), "-",
                SelectAll(), "-",
                .. AudioItems(s), "-",
                BrowseMissing(), Properties(),
            ],
            BoardTarget.TitleOverlay =>
            [
                .. Clipboard(s), "-",
                SelectAll(),
            ],
            _ =>
            [
                Paste(s), PlayBoard(), SelectAll(),
                Item(s.IsTimeline ? Strings.ContextClearTimeline : Strings.ContextClearStoryboard, Strings.PromptClearTimeline, s.ClearTimelineCommand, "Ctrl+Delete"),
            ],
        };

        var menu = new ContextMenu { Placement = PlacementMode.Pointer };
        foreach (object o in items)
        {
            menu.Items.Add(o as MenuItem ?? (object)new Separator());
        }

        return menu;
    }

    private static object[] Clipboard(ShellViewModel s) =>
    [
        Cut(s), Copy(s), Paste(s), Item(Strings.ContextRemove, Strings.PromptRemove, s.RemoveCommand, "Delete"),
    ];

    private static MenuItem Cut(ShellViewModel s) => Item(Strings.Cut, Strings.PromptCut, s.CutCommand, "Ctrl+X");

    private static MenuItem Copy(ShellViewModel s) => Item(Strings.Copy, Strings.PromptCopy, s.CopyCommand, "Ctrl+C");

    private static MenuItem Paste(ShellViewModel s) => Item(Strings.Paste, Strings.PromptPaste, s.PasteCommand, "Ctrl+V");

    private static object[] AudioItems(ShellViewModel s) =>
    [
        Check(Item(Strings.Mute, Strings.PromptMute, s.AudioMuteCommand), s.IsAudioMuted),
        Check(Item(Strings.FadeIn, Strings.PromptAudioFadeIn, s.AudioFadeInCommand), s.IsAudioFadeIn),
        Check(Item(Strings.FadeOut, Strings.PromptAudioFadeOut, s.AudioFadeOutCommand), s.IsAudioFadeOut),
        Item(Strings.Volume, Strings.PromptVolume, s.AudioVolumeCommand, "Ctrl+U"),
    ];

    private static MenuItem Check(MenuItem item, bool on)
    {
        item.ToggleType = MenuItemToggleType.CheckBox;
        item.IsChecked = on;
        return item;
    }

    private static MenuItem Item(string header, string? prompt, ICommand command, string? gesture = null)
    {
        var item = new MenuItem
        {
            Header = header,
            Command = command,
            InputGesture = gesture is null ? null : Menus.Shortcut.Parse(gesture).Gesture,
        };
        Menus.InWindowMenuHost.SetPrompt(item, prompt);
        return item;
    }
}
