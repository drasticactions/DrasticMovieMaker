namespace AvaMovieMaker.Effects.Titles;

public sealed record TitleFont
{
    public string Family { get; init => field = value ?? TitleFonts.DefaultFamily; } = TitleFonts.DefaultFamily;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public int SizeStep { get; init; }
}
