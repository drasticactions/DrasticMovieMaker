using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Planning;

public sealed record TitleSpan(MediaTime Start, MediaTime Length, TitleContent Content)
{
    public MediaTime End => Start + Length;
}
