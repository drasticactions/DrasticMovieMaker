using AvaMovieMaker.Timeline.Export;

namespace AvaMovieMaker.ViewModels.Publish;

public sealed record ProfileOption(PublishProfile Profile, string? MissingEncoder)
{
    public bool IsAvailable => MissingEncoder is null;

    public string Name => Profile.DisplayName;

    public string Reason => MissingEncoder is null ? string.Empty : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.NeedsEncoder, MissingEncoder);
}
