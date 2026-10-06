namespace AvaMovieMaker.ViewModels.Services;

public enum MessageButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel,
}

public enum MessageResult
{
    None,
    Ok,
    Cancel,
    Yes,
    No,
}

public enum MessageIcon
{
    None,
    Information,
    Warning,
    Error,
    Question,
}

public sealed record MessageRequest(string Text, MessageButtons Buttons = MessageButtons.Ok, MessageIcon Icon = MessageIcon.Information)
{
    public string Title { get; init; } = Strings.AppName;

    public string? DontShowAgainKey { get; init; }

    public bool IsList { get; init; }
}

public interface IMessageBoxes
{
    Task<MessageResult> ShowAsync(MessageRequest request);
}
