using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Media;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.AutoMovie;

public static class AutoMovieBuilder
{
    public const double MinimumSeconds = 29;

    private const double MaxMovieSeconds = 720;
    private const double TitleSeconds = 3, TitleOverlap = 0.5, CreditsSeconds = 10, EndCardSeconds = 5;
    private const double MinVideoSeconds = 4;

    private sealed record Candidate(AutoMovieSource Source, int Shot, double Start, double Length, double Quality, double Motion = 0)
    {
        public bool IsPicture => Source.Media.Kind == MediaKind.Picture;
    }

    public static AutoMovieOutcome Build(TimelineEditor editor, AutoMovieRequest request)
    {
        AutoMovieStyle style = request.Style;
        var random = new Random(request.Seed);
        ProjectSettings settings = editor.Project.Settings;
        Rational rate = settings.FrameRate;
        double picture = Math.Clamp(settings.PictureDuration.Seconds, 2, 15);
        double transition = Math.Min(1.0, Math.Max(0.25, Math.Min(settings.TransitionDuration.Seconds, 0.25 * picture)));

        List<AutoMovieSource> sources = [.. Sorted(request.Sources.Where(s => s.Media.Kind is MediaKind.Video or MediaKind.Picture))];
        double raw = sources.Sum(s => s.Media.Kind == MediaKind.Picture ? picture : s.Clip.Duration.Seconds);
        if (sources.Count == 0 || raw < MinimumSeconds)
        {
            return AutoMovieOutcome.NotEnoughContent;
        }

        double usableVideo = sources.Where(s => s.Media.Kind == MediaKind.Video)
            .SelectMany(Subshots)
            .Where(u => u.Quality > 0.1 && u.Length.Seconds >= MinVideoSeconds - 1e-4)
            .Sum(u => u.Length.Seconds);
        int usablePictures = sources.Count(UsablePicture);
        double usable = usableVideo + usablePictures * picture;
        if (usable < MinimumSeconds)
        {
            return AutoMovieOutcome.NoUsableContent;
        }

        MusicAnalysis? music = request.Music is null ? null : request.MusicAnalysis;
        if (request.Music is { } m && m.Duration.Seconds <= 30)
        {
            return AutoMovieOutcome.MusicTooShort;
        }

        if (request.Music is not null && (music is null || music.Length.Seconds < MinimumSeconds))
        {
            return AutoMovieOutcome.MusicTooQuiet;
        }

        double min = picture - transition, max = picture;
        double strength = music?.Beats.Sum(b => b.Strength) ?? 0;
        if (music is not null && strength > 0)
        {
            double c = music.Length.Seconds / strength;
            (double a, double b) = style.MusicVideo ? (3.0, 0.66) : (0.75 * picture, 0.5);
            double x = 1 + c;
            min = Math.Min(a * x, Math.Max(a, a * (1 - c / 2) + a * c * b / 0.8));
            max = min * x;
            if (style.MusicVideo)
            {
                transition = Math.Max(0.5, 0.1 * (min + max));
            }
        }

        double piece = max + transition + 0.2;
        if (EnoughFor(sources, piece, picture) < MinimumSeconds)
        {
            piece = MinVideoSeconds;
            if (EnoughFor(sources, piece, picture) < MinimumSeconds)
            {
                return AutoMovieOutcome.NotEnoughContent;
            }

            min = Math.Max(MinVideoSeconds, usableVideo / 20);
            max = 1.5 * min;
        }

        double span = music?.Length.Seconds ?? Math.Min(usable, MaxMovieSeconds);
        List<(double At, double Raw)> beats = music is not null
            ? [.. music.Beats.Select(b => ((b.Time - music.Start).Seconds, b.Raw))]
            : [.. Enumerable.Range(1, (int)(span / 0.25)).Select(i => (i * 0.25, 1.0))];
        List<double> slots = Slots(beats, span, min, max);

        List<List<Candidate>> groups = Groups(sources, piece);
        int candidates = groups.Sum(g => g.Count);
        if (candidates == 0)
        {
            return AutoMovieOutcome.NoUsableContent;
        }

        if (slots.Count > candidates)
        {
            slots.RemoveRange(candidates, slots.Count - candidates);
        }

        bool chronological = style.AlwaysChronological || min + max > 4;
        List<Candidate> picks = Pick(groups, Shares(groups, slots.Count), style, chronological);
        int count = Math.Min(picks.Count, slots.Count);

        var joins = new string?[count];
        int previous = -1;
        for (int i = 1; i < count && style.Transitions.Count > 0; i++)
        {
            int index = random.Next(style.Transitions.Count);
            if (index == previous)
            {
                index = (index + 1) % style.Transitions.Count;
            }

            previous = index;
            joins[i] = style.Transitions[index];
        }

        string? Effect() => style.Effects.Count > 0 ? style.Effects[random.Next(style.Effects.Count)] : null;
        MediaTime T(double seconds) => MediaTime.FromSeconds(seconds).SnapToFrame(rate);
        IReadOnlyList<EffectRef> Effects() => Effect() is { } fx ? [new EffectRef(fx)] : [];

        double videoShare = Math.Clamp((1 - request.AudioLevels) / 2, 0, 1);
        bool hasTitle = style.TitleAnimation is not null;
        var clips = new List<VideoClip>();
        MediaTime fade = T(transition);

        var cuts = new MediaTime[count + 1];
        double at = 0;
        for (int i = 0; i < count; i++)
        {
            cuts[i] = T(at);
            at += slots[i];
        }

        cuts[count] = T(at);
        for (int i = 0; i < count; i++)
        {
            Candidate c = picks[i];
            bool last = i == count - 1;
            double slot = slots[i];
            MediaTime length = cuts[i + 1] - cuts[i] + (!last && joins[i + 1] is not null ? fade : MediaTime.Zero);
            VideoClip clip = editor.NewVideoClip(c.Source.Media, c.Source.Clip);
            if (c.IsPicture)
            {
                clip = clip with { StillDuration = length };
            }
            else
            {
                double inPoint = last ? c.Start : c.Start + Math.Max(0, (c.Length - slot - transition) / 2);
                MediaTime segmentEnd = MediaTime.FromSeconds(c.Start + c.Length);
                length = MediaTime.Min(length, segmentEnd - MediaTime.FromSeconds(c.Start));
                MediaTime inTime = MediaTime.Min(T(inPoint), segmentEnd - length);
                clip = clip with { In = inTime, Out = inTime + length };
            }

            clip = clip with { Effects = Effects() };
            if (!c.IsPicture)
            {
                clip = clip with { Audio = clip.Audio with { Volume = videoShare, Mute = videoShare == 0 } };
            }

            if (i > 0 && joins[i] is { } id)
            {
                clip = clip with { TransitionIn = new TransitionRef(id, fade) };
            }
            else if (i == 0 && hasTitle)
            {
                clip = clip with { TransitionIn = new TransitionRef("fade", T(TitleOverlap)) };
            }

            clips.Add(clip);
        }

        if (clips.Count == 0)
        {
            return AutoMovieOutcome.NoUsableContent;
        }

        string author = request.Author;
        string title = !string.IsNullOrWhiteSpace(request.Title) ? request.Title.Trim()
            : System.IO.Path.GetFileNameWithoutExtension(picks[0].Source.Media.Path) is { Length: > 0 } n ? n : picks[0].Source.Media.Name;
        uint background = style.TitleBackground ?? new TitleContent().BackgroundColor;
        bool append = editor.Project.VideoTrack.Count > 0;

        VideoClip? opening = null;
        if (style.TitleAnimation is { } titleAnimation)
        {
            opening = editor.NewTitleClip(new TitleContent
            {
                Placement = append ? TitlePlacement.BeforeClip : TitlePlacement.AtBeginning,
                AnimationId = titleAnimation,
                Lines = [title],
                BackgroundColor = background,
            }) with { StillDuration = T(TitleSeconds), Effects = Effects() };
        }

        VideoClip? endCard = style.EndCard
            ? editor.NewTitleClip(new TitleContent
            {
                Placement = TitlePlacement.BeforeClip,
                AnimationId = "fade-in-and-out",
                Lines = [Strings.AutoMovieTheEnd],
                BackgroundColor = background,
            }) with { StillDuration = T(EndCardSeconds), TransitionIn = new TransitionRef("fade", T(TitleOverlap)), Effects = Effects() }
            : null;

        VideoClip? credits = style.CreditsAnimation is { } creditsAnimation
            ? editor.NewTitleClip(new TitleContent
            {
                Placement = TitlePlacement.CreditsAtEnd,
                AnimationId = creditsAnimation,
                Credits = style.Scoreboard
                    ? [new CreditRow(Strings.AutoMovieFinalScore, string.Empty), new CreditRow(Strings.AutoMovieHome, "0"), new CreditRow(Strings.AutoMovieVisitors, "0")]
                    : [new CreditRow(title, string.Empty), new CreditRow(Strings.AutoMovieDirectedBy, author), new CreditRow(Strings.AutoMovieCreatedWith, Strings.AutoMovieProduct)],
                BackgroundColor = background,
            }) with { StillDuration = T(CreditsSeconds), Effects = Effects() }
            : null;

        bool ok = editor.Batch(UndoNames.AutoMovie, p =>
        {
            if (opening is not null)
            {
                p.VideoTrack.Add(opening);
            }

            int firstClip = p.VideoTrack.Count;
            p.VideoTrack.AddRange(clips);
            int lastClip = p.VideoTrack.Count - 1;
            if (endCard is not null)
            {
                p.VideoTrack.Add(endCard);
            }

            if (credits is not null)
            {
                p.VideoTrack.Add(credits);
            }

            TimelineLayout layout = TimelineLayout.Compute(p);
            MediaTime clipStart = layout.Starts[firstClip], clipEnd = layout.Starts[lastClip] + p.VideoTrack[lastClip].Length;
            MediaTime clipSpan = clipEnd - clipStart;

            var overlays = new List<(MediaTime Start, MediaTime Length, TitleContent Content)>();
            if (style.Scoreboard)
            {
                var board = new TitleContent
                {
                    Placement = TitlePlacement.OnClip,
                    AnimationId = "sports-scoreboard",
                    Lines = [Strings.AutoMovieScoreboardHome, Strings.AutoMovieScoreboardVisitors],
                    BannerColor = 0x801010EF,
                };
                overlays.Add((clipStart + clipSpan / 3, T(5), board));
                overlays.Add((clipStart + clipSpan * 2 / 3, T(5), board));
            }

            if (style.MusicVideo)
            {
                var caption = new TitleContent
                {
                    Placement = TitlePlacement.OnClip,
                    AnimationId = "subtitle",
                    Lines = string.IsNullOrWhiteSpace(author) ? [title] : [title, author],
                    Alignment = TitleAlignment.Left,
                };
                if (clipSpan.Seconds > 5)
                {
                    overlays.Add((clipStart + T(1), T(4), caption));
                }

                if (clipSpan.Seconds > 10)
                {
                    overlays.Add((clipEnd - T(5), T(4), caption));
                }
            }

            foreach ((MediaTime start, MediaTime length, TitleContent content) in overlays)
            {
                if (!p.TitleOverlayTrack.Any(o => o.Start < start + length && start < o.End))
                {
                    p.TitleOverlayTrack.Add(new TitleClip { Start = start.SnapToFrame(rate), Duration = length, Content = content });
                }
            }

            p.TitleOverlayTrack.Sort((a, b) => a.Start.CompareTo(b.Start));

            if (request.Music is { } song && music is not null)
            {
                MediaTime length = MediaTime.Min(music.End - music.Start, clipEnd - clipStart);
                p.AudioMusicTrack.RemoveAll(a => a.Start < clipStart + length && clipStart < a.End);
                p.AudioMusicTrack.Add(new AudioClip
                {
                    MediaId = song.Id,
                    SourceClipId = song.Clips.FirstOrDefault()?.Id ?? Guid.Empty,
                    In = music.Start,
                    Out = music.Start + length,
                    Start = clipStart,
                    Audio = new AudioSettings { Volume = 1 - videoShare, FadeOut = true },
                });
                p.AudioMusicTrack.Sort((a, b) => a.Start.CompareTo(b.Start));
            }

            p.AutoMovieSeed = request.Seed;
        });
        return ok ? AutoMovieOutcome.Created : AutoMovieOutcome.NotEnoughContent;
    }

    private static IEnumerable<AutoMovieSource> Sorted(IEnumerable<AutoMovieSource> sources) => sources
        .OrderBy(s => s.Media.DateTaken?.UtcDateTime ?? DateTime.MaxValue)
        .ThenBy(s => s.Media.Name, StringComparer.CurrentCulture)
        .ThenBy(s => s.Clip.Start);

    private static IEnumerable<Subshot> Subshots(AutoMovieSource s) =>
        s.Analysis is { } a ? a.Subshots : [new Subshot(0, s.Clip.Start, s.Clip.Duration, 1)];

    private static bool UsablePicture(AutoMovieSource s) =>
        s.Media.Kind == MediaKind.Picture
        && !s.Media.Path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
        && (s.Analysis?.PictureQuality ?? 1) > 0.25;

    private static double EnoughFor(List<AutoMovieSource> sources, double piece, double picture) =>
        sources.Count(UsablePicture) * piece
        + sources.Where(s => s.Media.Kind == MediaKind.Video).SelectMany(Subshots)
            .Where(u => u.Quality > 0.1 && u.Length.Seconds >= piece - 1e-4).Sum(u => u.Length.Seconds);

    private static List<double> Slots(List<(double At, double Raw)> beats, double span, double min, double max)
    {
        var slots = new List<double>();
        double t = 0;
        int from = 0;
        while (span - t > 0.5)
        {
            double cut = t + max, best = -1;
            for (int i = from; i < beats.Count && beats[i].At < t + max; i++)
            {
                if (beats[i].At >= t + min - 1e-9 && beats[i].Raw > best)
                {
                    (cut, best) = (beats[i].At, beats[i].Raw);
                }
            }

            cut = Math.Min(cut, span);
            slots.Add(cut - t);
            t = cut;
            while (from < beats.Count && beats[from].At <= t)
            {
                from++;
            }
        }

        return slots;
    }

    private static List<List<Candidate>> Groups(List<AutoMovieSource> sources, double piece)
    {
        var groups = new List<List<Candidate>>();
        foreach (AutoMovieSource s in sources)
        {
            if (s.Media.Kind == MediaKind.Picture)
            {
                if (UsablePicture(s))
                {
                    groups.Add([new Candidate(s, 0, s.Clip.Start.Seconds, double.PositiveInfinity, s.Analysis?.PictureQuality ?? 1)]);
                }

                continue;
            }

            var segments = new List<Candidate>();
            foreach (IGrouping<int, Subshot> shot in Subshots(s).GroupBy(u => u.Shot))
            {
                var merged = new List<(double Start, double Length, double Weighted, double Motion)>();
                (double Start, double Length, double Weighted, double Motion)? open = null;
                foreach (Subshot u in shot.Where(u => u.Quality > 0.1).OrderBy(u => u.Start))
                {
                    if (open is { } gapped && u.Start.Seconds - (gapped.Start + gapped.Length) > 0.5)
                    {
                        open = null;
                    }

                    double len = u.Length.Seconds;
                    open = open is { } o
                        ? (o.Start, u.End.Seconds - o.Start, o.Weighted + u.Quality * len, o.Motion + u.Motion * len)
                        : (u.Start.Seconds, len, u.Quality * len, u.Motion * len);
                    if (open.Value.Length >= piece)
                    {
                        merged.Add(open.Value);
                        open = null;
                    }
                }

                if (open is { } rest && merged.Count > 0 && rest.Start - (merged[^1].Start + merged[^1].Length) <= 0.5)
                {
                    var lastSegment = merged[^1];
                    merged[^1] = (lastSegment.Start, rest.Start + rest.Length - lastSegment.Start, lastSegment.Weighted + rest.Weighted, lastSegment.Motion + rest.Motion);
                }

                foreach ((double start, double length, double weighted, double motion) in merged)
                {
                    double q = weighted / length;
                    if (q > 0.1 && length >= piece - 1e-4)
                    {
                        segments.Add(new Candidate(s, shot.Key, start, length, q, motion / length));
                    }
                }
            }

            int shots = Subshots(s).Select(u => u.Shot).Distinct().Count();
            if (shots < 2 || segments.Count == 0)
            {
                if (segments.Count > 0)
                {
                    groups.Add(segments);
                }

                continue;
            }

            int k = Math.Max(2, (int)Math.Round(s.Clip.Duration.Seconds / 3600 * 10));
            double size = s.Clip.Duration.Seconds / k;
            groups.AddRange(segments.GroupBy(c => Math.Min(k - 1, (int)((c.Start - s.Clip.Start.Seconds) / size))).OrderBy(g => g.Key).Select(g => g.ToList()));
        }

        return groups;
    }

    private static int[] Shares(List<List<Candidate>> groups, int slots)
    {
        int[] shares = new int[groups.Count];
        int total = groups.Sum(g => g.Count);
        if (slots < groups.Count)
        {
            foreach (int g in Enumerable.Range(0, groups.Count).OrderByDescending(g => groups[g].Count).ThenBy(g => g).Take(slots))
            {
                shares[g] = 1;
            }

            return shares;
        }

        for (int g = 0; g < groups.Count; g++)
        {
            shares[g] = Math.Min(groups[g].Count, Math.Max(1, (int)((double)slots * groups[g].Count / total)));
        }

        while (shares.Sum() < slots)
        {
            int g = Enumerable.Range(0, groups.Count).Where(g => shares[g] < groups[g].Count).OrderByDescending(g => groups[g].Count - shares[g]).ThenBy(g => g).FirstOrDefault(-1);
            if (g < 0)
            {
                break;
            }

            shares[g]++;
        }

        while (shares.Sum() > slots)
        {
            shares[Enumerable.Range(0, groups.Count).OrderByDescending(g => shares[g]).ThenByDescending(g => g).First()]--;
        }

        return shares;
    }

    private static List<Candidate> Pick(List<List<Candidate>> groups, int[] shares, AutoMovieStyle style, bool chronological)
    {
        var picks = new List<Candidate>();
        for (int g = 0; g < groups.Count; g++)
        {
            List<Candidate> group = groups[g];
            double[] score = [.. group.Select(c => !style.AllowShaky && c.Motion > 1.5 ? -1000 : style.QualityWeight * c.Quality + (1 - style.QualityWeight) * c.Motion)];
            var chosen = new List<int>();
            for (int n = 0; n < shares[g]; n++)
            {
                int best = 0;
                for (int i = 1; i < group.Count; i++)
                {
                    if (score[i] > score[best])
                    {
                        best = i;
                    }
                }

                chosen.Add(best);
                score[best] = double.NegativeInfinity;
                for (int i = 0; i < group.Count; i++)
                {
                    if (i != best && group[i].Shot == group[best].Shot && group[i].Source == group[best].Source)
                    {
                        score[i] -= Math.Abs(i - best) == 1 ? 2 : 1;
                    }
                }
            }

            picks.AddRange((chronological ? chosen.Order() : (IEnumerable<int>)chosen).Select(i => group[i]));
        }

        return picks;
    }
}
