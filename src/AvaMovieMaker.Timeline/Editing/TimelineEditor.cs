using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Media;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Undo;

namespace AvaMovieMaker.Timeline.Editing;

public sealed class TimelineEditor(Project project, UndoStack undo)
{
    public const int MaxEffects = 6;

    public static string TransitionDoesNotFit => Strings.TransitionDoesNotFit;

    public event Action<string>? Refused;

    public static readonly MediaTime MinimumLength = MediaTime.FromSeconds(1.0 / 30);

    public Project Project { get; } = project;

    public UndoStack Undo { get; } = undo;

    private bool Run(string name, Action<Project> change, string? mergeKey = null)
    {
        Undo.Execute(new ProjectEdit(Project, name, change, mergeKey));
        return true;
    }

    private Rational Rate => Project.Settings.FrameRate;

    public bool ImportMedia(IReadOnlyList<MediaItem> items)
    {
        if (items.Count == 0)
        {
            return false;
        }

        return Run(UndoNames.ImportMediaItems, p => p.Media.AddRange(items));
    }

    public bool RemoveMedia(IReadOnlyCollection<Guid> mediaIds)
    {
        if (mediaIds.Count == 0)
        {
            return false;
        }

        return Run(UndoNames.RemoveClip, p =>
        {
            p.Media.RemoveAll(m => mediaIds.Contains(m.Id));
            RemoveVideo(p, p.VideoTrack.Where(c => c.Kind != VideoClipKind.Title && mediaIds.Contains(c.MediaId)).Select(c => c.Id).ToHashSet());
            p.AudioMusicTrack.RemoveAll(a => mediaIds.Contains(a.MediaId));
        });
    }

    public bool RemoveSourceClips(Guid mediaId, IReadOnlyCollection<Guid> clipIds)
    {
        MediaItem? m = Project.FindMedia(mediaId);
        if (m is null)
        {
            return false;
        }

        if (m.Clips.All(c => clipIds.Contains(c.Id)))
        {
            return RemoveMedia([mediaId]);
        }

        return Run(UndoNames.RemoveClip, p => Replace(p, m with { Clips = m.Clips.Where(c => !clipIds.Contains(c.Id)).ToList() }));
    }

    public bool RenameMedia(Guid mediaId, Guid clipId, string name)
    {
        MediaItem? m = Project.FindMedia(mediaId);
        name = name.Trim();
        if (m is null || name.Length == 0)
        {
            return false;
        }

        return Run(UndoNames.RenameClip, p =>
        {
            if (m.Clips.Count > 1 && clipId != Guid.Empty)
            {
                Replace(p, m with { Clips = m.Clips.Select(c => c.Id == clipId ? c with { Name = name } : c).ToList() });
            }
            else
            {
                Replace(p, m with { Name = name, Clips = m.Clips.Select(c => c with { Name = name }).ToList() });
            }
        });
    }

    public bool CombineSourceClips(Guid mediaId, IReadOnlyList<Guid> clipIds)
    {
        MediaItem? m = Project.FindMedia(mediaId);
        if (m is null || clipIds.Count < 2)
        {
            return false;
        }

        var sel = m.Clips.Where(c => clipIds.Contains(c.Id)).OrderBy(c => c.Start).ToList();
        for (int i = 1; i < sel.Count; i++)
        {
            if (sel[i].Start != sel[i - 1].End)
            {
                return false;
            }
        }

        var merged = sel[0] with { End = sel[^1].End };
        var clips = m.Clips.Where(c => !clipIds.Contains(c.Id) || c.Id == sel[0].Id).Select(c => c.Id == sel[0].Id ? merged : c).ToList();
        return Run(UndoNames.Combine, p => Replace(p, m with { Clips = clips }));
    }

    public bool SetSourceClips(Guid mediaId, IReadOnlyList<SourceClip> clips)
    {
        MediaItem? m = Project.FindMedia(mediaId);
        if (m is null || clips.Count == 0)
        {
            return false;
        }

        return Run(UndoNames.CreateClips, p => Replace(p, m with { Clips = clips }));
    }

    public bool Relink(Guid mediaId, string newPath)
    {
        MediaItem? m = Project.FindMedia(mediaId);
        if (m is null)
        {
            return false;
        }

        return Run(UndoNames.SetProperty, p => Replace(p, m with { Path = newPath, Missing = false }));
    }

    private static void Replace(Project p, MediaItem m)
    {
        int i = p.Media.FindIndex(x => x.Id == m.Id);
        if (i >= 0)
        {
            p.Media[i] = m;
        }
    }

    public VideoClip NewVideoClip(MediaItem m, SourceClip? clip = null)
    {
        clip ??= m.Clips.FirstOrDefault();
        if (m.Kind == MediaKind.Picture)
        {
            return new VideoClip
            {
                Kind = VideoClipKind.Picture,
                MediaId = m.Id,
                SourceClipId = clip?.Id ?? Guid.Empty,
                StillDuration = Project.Settings.PictureDuration,
            };
        }

        MediaTime start = clip?.Start ?? MediaTime.Zero;
        MediaTime end = clip?.End ?? m.Duration;
        return new VideoClip { Kind = VideoClipKind.Video, MediaId = m.Id, SourceClipId = clip?.Id ?? Guid.Empty, In = start, Out = end };
    }

    public bool InsertVideoClips(int index, IReadOnlyList<VideoClip> clips, string? name = null)
    {
        if (clips.Count == 0)
        {
            return false;
        }

        index = Math.Clamp(index, 0, Project.VideoTrack.Count);
        return Run(name ?? UndoNames.AddClip, p =>
        {
            p.VideoTrack.InsertRange(index, clips);

            if (index == 0 && p.VideoTrack.Count > 0 && p.VideoTrack[0].TransitionIn is not null)
            {
                p.VideoTrack[0] = p.VideoTrack[0] with { TransitionIn = null };
            }
        });
    }

    public bool AddToTimeline(IReadOnlyList<(MediaItem Media, SourceClip? Clip)> items)
    {
        if (items.Count == 0)
        {
            return false;
        }

        var video = new List<VideoClip>();
        var audio = new List<(MediaItem, SourceClip?)>();
        foreach ((MediaItem m, SourceClip? c) in items)
        {
            if (m.Kind == MediaKind.Audio)
            {
                audio.Add((m, c));
            }
            else
            {
                video.Add(NewVideoClip(m, c));
            }
        }

        return Run(UndoNames.AddClip, p =>
        {
            p.VideoTrack.AddRange(video);
            MediaTime at = p.AudioMusicTrack.Count == 0 ? MediaTime.Zero : p.AudioMusicTrack.Max(a => a.End);
            foreach ((MediaItem m, SourceClip? c) in audio)
            {
                AudioClip clip = NewAudioClip(m, c, at);
                p.AudioMusicTrack.Add(clip);
                at = clip.End;
            }

            SortAudio(p);
        });
    }

    public bool RemoveClips(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0 || !ids.Any(Contains))
        {
            return false;
        }

        return Run(UndoNames.RemoveClip, p =>
        {
            RemoveVideo(p, ids);
            p.AudioMusicTrack.RemoveAll(a => ids.Contains(a.Id));
            p.TitleOverlayTrack.RemoveAll(t => ids.Contains(t.Id));
        });
    }

    private bool Contains(Guid id) =>
        Project.VideoTrack.Any(c => c.Id == id) || Project.AudioMusicTrack.Any(a => a.Id == id) || Project.TitleOverlayTrack.Any(t => t.Id == id);

    private static void RemoveVideo(Project p, IReadOnlyCollection<Guid> ids)
    {
        p.VideoTrack.RemoveAll(c => ids.Contains(c.Id));
        if (p.VideoTrack.Count > 0 && p.VideoTrack[0].TransitionIn is not null)
        {
            p.VideoTrack[0] = p.VideoTrack[0] with { TransitionIn = null };
        }
    }

    public bool MoveVideoClips(IReadOnlyCollection<Guid> ids, int targetIndex)
    {
        var moving = Project.VideoTrack.Where(c => ids.Contains(c.Id)).ToList();
        if (moving.Count == 0)
        {
            return false;
        }

        int before = Project.VideoTrack.Take(Math.Clamp(targetIndex, 0, Project.VideoTrack.Count)).Count(c => ids.Contains(c.Id));
        int insertAt = Math.Clamp(targetIndex - before, 0, Project.VideoTrack.Count - moving.Count);
        var rest = Project.VideoTrack.Where(c => !ids.Contains(c.Id)).ToList();
        var result = new List<VideoClip>(rest);
        result.InsertRange(insertAt, moving);
        if (result.SequenceEqual(Project.VideoTrack))
        {
            return false;
        }

        return Run(UndoNames.MoveClip, p =>
        {
            p.VideoTrack = result;
            if (p.VideoTrack[0].TransitionIn is not null)
            {
                p.VideoTrack[0] = p.VideoTrack[0] with { TransitionIn = null };
            }
        });
    }

    private bool UpdateVideo(Guid id, string name, Func<VideoClip, VideoClip?> change, string? mergeKey = null)
    {
        int i = Project.IndexOfVideo(id);
        if (i < 0)
        {
            return false;
        }

        VideoClip? next = change(Project.VideoTrack[i]);
        if (next is null || next == Project.VideoTrack[i])
        {
            return false;
        }

        return Run(name, p =>
        {
            int j = p.IndexOfVideo(id);
            p.VideoTrack[j] = next;
        }, mergeKey);
    }

    public (MediaTime Start, MediaTime End) SourceRange(VideoClip c)
    {
        MediaItem? m = Project.FindMedia(c.MediaId);
        SourceClip? sc = m?.Clip(c.SourceClipId);
        if (sc is not null)
        {
            return (sc.Start, sc.End);
        }

        return (MediaTime.Zero, m?.Duration ?? c.Out);
    }

    public bool TrimVideo(Guid id, MediaTime newIn, MediaTime newOut, string? mergeKey = null)
    {
        int i = Project.IndexOfVideo(id);
        if (i < 0 || Project.VideoTrack[i].Kind != VideoClipKind.Video)
        {
            return false;
        }

        VideoClip c = Project.VideoTrack[i];
        (MediaTime lo, MediaTime hi) = SourceRange(c);
        Rational srcRate = SourceRate(c.MediaId);
        newIn = newIn.SnapToFrame(srcRate).Clamp(lo, hi - MinimumLength);
        newOut = newOut.SnapToFrame(srcRate).Clamp(newIn + MinimumLength, hi);
        if (newOut > hi)
        {
            newOut = hi;
        }

        return UpdateVideo(id, UndoNames.TrimClip, x => x with { In = newIn, Out = newOut }, mergeKey);
    }

    public bool SetStillDuration(Guid id, MediaTime duration, string? mergeKey = null)
    {
        duration = MediaTime.Max(MinimumLength, duration.SnapToFrame(Rate));
        return UpdateVideo(id, mergeKey is null ? UndoNames.SetDuration : UndoNames.TrimClip,
            c => c.IsStill ? c with { StillDuration = duration } : null, mergeKey);
    }

    public bool TrimStartAt(MediaTime playhead) => TrimAtPlayhead(playhead, start: true);

    public bool TrimEndAt(MediaTime playhead) => TrimAtPlayhead(playhead, start: false);

    private bool TrimAtPlayhead(MediaTime playhead, bool start)
    {
        TimelineLayout layout = TimelineLayout.Compute(Project);
        int i = layout.IndexAt(playhead);
        if (i < 0)
        {
            return TrimOtherTrackAt(playhead, start);
        }

        VideoClip c = Project.VideoTrack[i];
        MediaTime offset = playhead - layout.Starts[i];
        if (offset <= MediaTime.Zero || offset >= layout.Lengths[i])
        {
            return false;
        }

        string name = start ? UndoNames.TrimStart : UndoNames.TrimEnd;
        if (c.IsStill)
        {
            MediaTime d = start ? layout.Lengths[i] - offset : offset;
            return UpdateVideo(c.Id, start ? name : UndoNames.SetDuration, x => x with { StillDuration = d });
        }

        MediaTime src = c.In + offset * c.Speed;
        return UpdateVideo(c.Id, name, x => start ? x with { In = src } : x with { Out = src });
    }

    private bool TrimOtherTrackAt(MediaTime playhead, bool start)
    {
        AudioClip? a = Project.AudioMusicTrack.FirstOrDefault(x => playhead > x.Start && playhead < x.End);
        if (a is null)
        {
            if (!start && Project.TitleOverlayTrack.FirstOrDefault(x => playhead > x.Start && playhead < x.End) is { } t)
            {
                return Run(UndoNames.SetDuration, p =>
                {
                    int k = p.TitleOverlayTrack.FindIndex(x => x.Id == t.Id);
                    p.TitleOverlayTrack[k] = t with { Duration = playhead - t.Start };
                });
            }

            return false;
        }

        MediaTime offset = playhead - a.Start;
        return Run(start ? UndoNames.TrimStart : UndoNames.TrimEnd, p =>
        {
            int j = p.AudioMusicTrack.FindIndex(x => x.Id == a.Id);
            p.AudioMusicTrack[j] = start ? a with { In = a.In + offset, Start = playhead } : a with { Out = a.In + offset };
        });
    }

    public bool ClearTrimPoints(Guid id)
    {
        int i = Project.IndexOfVideo(id);
        if (i >= 0)
        {
            VideoClip c = Project.VideoTrack[i];
            if (c.IsStill)
            {
                return false;
            }

            (MediaTime lo, MediaTime hi) = SourceRange(c);
            (lo, hi) = (c.RangeIn ?? lo, c.RangeOut ?? hi);
            return UpdateVideo(id, UndoNames.ClearTrim, x => x with { In = lo, Out = hi });
        }

        AudioClip? a = Project.AudioMusicTrack.FirstOrDefault(x => x.Id == id);
        MediaItem? m = a is null ? null : Project.FindMedia(a.MediaId);
        if (a is null || m is null)
        {
            return false;
        }

        SourceClip? sc = m.Clip(a.SourceClipId);
        MediaTime lo2 = a.RangeIn ?? sc?.Start ?? MediaTime.Zero, hi2 = a.RangeOut ?? sc?.End ?? m.Duration;
        return Run(UndoNames.ClearTrim, p =>
        {
            int j = p.AudioMusicTrack.FindIndex(x => x.Id == id);
            p.AudioMusicTrack[j] = a with { In = lo2, Out = hi2 };
            ResolveAudioOverlaps(p, id);
        });
    }

    public bool SplitAt(MediaTime playhead, Guid? preferTrackClip = null)
    {
        MediaTime frame = MediaTime.FrameDuration(Rate);
        MediaTime at = CeilToFrame(playhead);

        if (preferTrackClip is { } pid && Project.AudioMusicTrack.FirstOrDefault(a => a.Id == pid) is { } ac && at > ac.Start && at < ac.End)
        {
            return at - ac.Start >= frame && ac.End - at >= frame && SplitAudio(ac, at);
        }

        TimelineLayout layout = TimelineLayout.Compute(Project);
        int i = layout.IndexAt(playhead);
        if (i < 0 || Project.VideoTrack[i].IsStill)
        {
            return false;
        }

        VideoClip c = Project.VideoTrack[i];
        MediaTime offset = at - layout.Starts[i];
        bool inOverlap = (i > 0 && at < layout.End(i - 1) + frame) || (i + 1 < layout.Count && at > layout.Starts[i + 1] - frame);
        if (offset < frame || layout.Lengths[i] - offset < frame || inOverlap)
        {
            return false;
        }

        MediaTime src = c.In + offset * c.Speed;
        (MediaTime lo, MediaTime hi) = SourceRange(c);
        VideoClip first = c with { Out = src, RangeIn = c.RangeIn ?? lo, RangeOut = src, VideoFadeOut = false, Audio = c.Audio with { FadeOut = false } };
        VideoClip second = c with
        {
            Id = Guid.NewGuid(), In = src, RangeIn = src, RangeOut = c.RangeOut ?? hi,
            TransitionIn = null, VideoFadeIn = false, Audio = c.Audio with { FadeIn = false },
        };
        return Run(UndoNames.Split, p =>
        {
            int j = p.IndexOfVideo(c.Id);
            p.VideoTrack[j] = first;
            p.VideoTrack.Insert(j + 1, second);
        });
    }

    private MediaTime CeilToFrame(MediaTime t)
    {
        MediaTime snapped = t.SnapToFrame(Rate);
        return snapped < t ? snapped + MediaTime.FrameDuration(Rate) : snapped;
    }

    private bool SplitAudio(AudioClip a, MediaTime playhead)
    {
        MediaTime offset = playhead - a.Start;
        AudioSourceRange(a, out MediaTime lo, out MediaTime hi);
        var first = a with { Out = a.In + offset, RangeIn = a.RangeIn ?? lo, RangeOut = a.In + offset, Audio = a.Audio with { FadeOut = false } };
        var second = a with { Id = Guid.NewGuid(), In = a.In + offset, Start = playhead, RangeIn = a.In + offset, RangeOut = a.RangeOut ?? hi, Audio = a.Audio with { FadeIn = false } };
        return Run(UndoNames.Split, p =>
        {
            int j = p.AudioMusicTrack.FindIndex(x => x.Id == a.Id);
            p.AudioMusicTrack[j] = first;
            p.AudioMusicTrack.Insert(j + 1, second);
        });
    }

    private void AudioSourceRange(AudioClip a, out MediaTime lo, out MediaTime hi)
    {
        MediaItem? m = Project.FindMedia(a.MediaId);
        SourceClip? sc = m?.Clip(a.SourceClipId);
        lo = sc?.Start ?? MediaTime.Zero;
        hi = sc?.End ?? m?.Duration ?? a.Out;
    }

    public bool Combine(IReadOnlyCollection<Guid> ids)
    {
        MediaTime nearTimeline = MediaTime.FromSeconds(0.1), nearSource = MediaTime.FromSeconds(0.5);
        bool SameFile(Guid a, Guid b) => Project.FindMedia(a) is { } ma && Project.FindMedia(b) is { } mb && (ma.Id == mb.Id || ma.Path == mb.Path);
        static MediaTime Gap(MediaTime x, MediaTime y) => x > y ? x - y : y - x;

        var idx = ids.Select(Project.IndexOfVideo).Where(i => i >= 0).Order().ToList();
        if (idx.Count >= 2)
        {
            TimelineLayout layout = TimelineLayout.Compute(Project);
            for (int k = 1; k < idx.Count; k++)
            {
                VideoClip a = Project.VideoTrack[idx[k - 1]], b = Project.VideoTrack[idx[k]];
                bool ok = idx[k] == idx[k - 1] + 1 && a.Kind == VideoClipKind.Video && b.Kind == VideoClipKind.Video && SameFile(a.MediaId, b.MediaId)
                    && (b.TransitionIn is null || b.TransitionIn.Duration < nearTimeline) && Gap(a.Out, b.In) < nearSource;
                if (!ok)
                {
                    return false;
                }
            }

            VideoClip head = Project.VideoTrack[idx[0]], tail = Project.VideoTrack[idx[^1]];
            MediaTime length = layout.End(idx[^1]) - layout.Starts[idx[0]];
            (_, MediaTime hi) = SourceRange(head);
            VideoClip merged = head with
            {
                Out = MediaTime.Min(hi, head.In + length * head.Speed),
                RangeIn = head.RangeIn,
                RangeOut = tail.RangeOut,
            };
            var remove = idx.Skip(1).Select(i => Project.VideoTrack[i].Id).ToHashSet();
            return Run(UndoNames.Combine, p =>
            {
                p.VideoTrack[p.IndexOfVideo(head.Id)] = merged;
                p.VideoTrack.RemoveAll(c => remove.Contains(c.Id));
            });
        }

        var audio = Project.AudioMusicTrack.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Start).ToList();
        if (audio.Count < 2)
        {
            return false;
        }

        for (int k = 1; k < audio.Count; k++)
        {
            AudioClip a = audio[k - 1], b = audio[k];
            if (!SameFile(a.MediaId, b.MediaId) || Gap(a.End, b.Start) >= nearTimeline || Gap(a.Out, b.In) >= nearSource)
            {
                return false;
            }
        }

        AudioClip first = audio[0], last = audio[^1];
        AudioClip joined = first with { Out = first.In + (last.End - first.Start), RangeIn = first.RangeIn, RangeOut = last.RangeOut };
        var gone = audio.Skip(1).Select(x => x.Id).ToHashSet();
        return Run(UndoNames.Combine, p =>
        {
            p.AudioMusicTrack[p.AudioMusicTrack.FindIndex(x => x.Id == first.Id)] = joined;
            p.AudioMusicTrack.RemoveAll(x => gone.Contains(x.Id));
        });
    }

    public MediaTime MaxTransition(int index)
    {
        if (index <= 0 || index >= Project.VideoTrack.Count)
        {
            return MediaTime.Zero;
        }

        TimelineLayout layout = TimelineLayout.Compute(Project);
        return MediaTime.Min(layout.Lengths[index - 1] - layout.Transitions[index - 1], layout.Lengths[index]);
    }

    public bool TransitionFits(Guid clipId)
    {
        int i = Project.IndexOfVideo(clipId);
        if (i <= 0)
        {
            return false;
        }

        if (Project.VideoTrack[i].TransitionIn is not null)
        {
            return true;
        }

        TimelineLayout layout = TimelineLayout.Compute(Project);
        return Project.Settings.TransitionDuration < MediaTime.Min(layout.Lengths[i - 1], layout.Lengths[i]);
    }

    public bool CopyVideoClips(IReadOnlyCollection<Guid> ids, int index)
    {
        var copies = Project.VideoTrack.Where(c => ids.Contains(c.Id)).Select(c => c with { Id = Guid.NewGuid() }).ToList();
        return copies.Count > 0 && InsertVideoClips(Math.Clamp(index, 0, Project.VideoTrack.Count), copies);
    }

    public bool SetTransition(Guid clipId, string transitionId, MediaTime? duration = null)
    {
        int i = Project.IndexOfVideo(clipId);
        if (i <= 0 || TransitionCatalog.Find(transitionId) is null)
        {
            return false;
        }

        TransitionRef? existing = Project.VideoTrack[i].TransitionIn;
        MediaTime d = duration ?? existing?.Duration ?? Project.Settings.TransitionDuration;
        if (duration is null && existing is null)
        {
            TimelineLayout layout = TimelineLayout.Compute(Project);
            if (d >= MediaTime.Min(layout.Lengths[i - 1], layout.Lengths[i]))
            {
                Refused?.Invoke(TransitionDoesNotFit);
                return false;
            }
        }

        d = MediaTime.Min(d, MaxTransition(i));
        if (d <= MediaTime.Zero)
        {
            return false;
        }

        return UpdateVideo(clipId, existing is null ? UndoNames.AddTransition : UndoNames.ChangeTransition, c => c with { TransitionIn = new TransitionRef(transitionId, d) });
    }

    public bool SetTransitionDuration(Guid clipId, MediaTime duration, string? mergeKey = null)
    {
        int i = Project.IndexOfVideo(clipId);
        if (i <= 0 || Project.VideoTrack[i].TransitionIn is not { } t)
        {
            return false;
        }

        duration = duration.SnapToFrame(Rate).Clamp(MediaTime.FrameDuration(Rate), MaxTransition(i));
        return UpdateVideo(clipId, UndoNames.TrimTransition, c => c with { TransitionIn = t with { Duration = duration } }, mergeKey);
    }

    public bool OverlapToTransition(Guid clipId, MediaTime overlap, string? mergeKey = null)
    {
        int i = Project.IndexOfVideo(clipId);
        if (i <= 0)
        {
            return false;
        }

        if (overlap <= MediaTime.Zero)
        {
            return UpdateVideo(clipId, UndoNames.RemoveTransition, c => c.TransitionIn is null ? null : c with { TransitionIn = null }, mergeKey);
        }

        overlap = overlap.SnapToFrame(Rate).Clamp(MediaTime.FrameDuration(Rate), MaxTransition(i));
        string id = Project.VideoTrack[i].TransitionIn?.TransitionId ?? TransitionCatalog.FallbackId;
        return UpdateVideo(clipId, UndoNames.TrimTransition, c => c with { TransitionIn = new TransitionRef(id, overlap) }, mergeKey);
    }

    public bool RemoveTransition(Guid clipId) =>
        UpdateVideo(clipId, UndoNames.RemoveTransition, c => c.TransitionIn is null ? null : c with { TransitionIn = null });

    public bool AddEffect(IReadOnlyCollection<Guid> clipIds, string effectId)
    {
        if (EffectCatalog.Find(effectId) is null)
        {
            return false;
        }

        var targets = clipIds.Where(id => Project.IndexOfVideo(id) >= 0 && Project.VideoTrack[Project.IndexOfVideo(id)].Effects.Count < MaxEffects).ToList();
        if (targets.Count == 0)
        {
            return false;
        }

        return Run(UndoNames.AddEffect, p =>
        {
            foreach (Guid id in targets)
            {
                int j = p.IndexOfVideo(id);
                VideoClip c = p.VideoTrack[j];
                p.VideoTrack[j] = c with { Effects = [.. c.Effects, new EffectRef(effectId)] };
            }
        });
    }

    public bool RemoveEffects(IReadOnlyCollection<Guid> clipIds)
    {
        var targets = clipIds.Where(id => Project.IndexOfVideo(id) >= 0 && Project.VideoTrack[Project.IndexOfVideo(id)].Effects.Count > 0).ToList();
        if (targets.Count == 0)
        {
            return false;
        }

        return Run(UndoNames.RemoveEffects, p =>
        {
            foreach (Guid id in targets)
            {
                int j = p.IndexOfVideo(id);
                p.VideoTrack[j] = p.VideoTrack[j] with { Effects = [] };
            }
        });
    }

    public bool SetEffects(Guid clipId, IReadOnlyList<string> effectIds) =>
        UpdateVideo(clipId, UndoNames.ChangeEffects, c => c with { Effects = effectIds.Where(e => EffectCatalog.Find(e) is not null).Select(e => new EffectRef(e)).ToList() });

    public bool ToggleEffect(Guid clipId, string effectId)
    {
        int i = Project.IndexOfVideo(clipId);
        if (i < 0 || EffectCatalog.Find(effectId) is null)
        {
            return false;
        }

        VideoClip c = Project.VideoTrack[i];
        int at = c.Effects.ToList().FindIndex(e => e.EffectId == effectId);
        if (at >= 0)
        {
            return UpdateVideo(clipId, UndoNames.RemoveEffect, x => x with { Effects = [.. x.Effects.Where((_, k) => k != at)] });
        }

        return c.Effects.Count < MaxEffects && UpdateVideo(clipId, UndoNames.AddEffect, x => x with { Effects = [.. x.Effects, new EffectRef(effectId)] });
    }

    public bool SetVideoFade(Guid clipId, bool? fadeIn, bool? fadeOut) =>
        UpdateVideo(clipId, UndoNames.SetProperty, c => c with { VideoFadeIn = fadeIn ?? c.VideoFadeIn, VideoFadeOut = fadeOut ?? c.VideoFadeOut });

    public bool SetAudio(Guid clipId, AudioSettings settings)
    {
        settings = settings with { Volume = Math.Clamp(settings.Volume, 0, AudioSettings.MaxVolume) };
        if (Project.IndexOfVideo(clipId) >= 0)
        {
            string videoName = AudioUndoName(Project.VideoTrack[Project.IndexOfVideo(clipId)].Audio, settings);
            return UpdateVideo(clipId, videoName, c => c with { Audio = settings });
        }

        AudioClip? a = Project.AudioMusicTrack.FirstOrDefault(x => x.Id == clipId);
        if (a is null || a.Audio == settings)
        {
            return false;
        }

        return Run(AudioUndoName(a.Audio, settings), p =>
        {
            int j = p.AudioMusicTrack.FindIndex(x => x.Id == clipId);
            p.AudioMusicTrack[j] = a with { Audio = settings };
        });
    }

    private static string AudioUndoName(AudioSettings before, AudioSettings after) =>
        before.Mute != after.Mute ? UndoNames.MuteItem
        : before.FadeIn != after.FadeIn ? UndoNames.AudioFadeIn
        : before.FadeOut != after.FadeOut ? UndoNames.AudioFadeOut
        : UndoNames.ClipVolume;

    public AudioClip NewAudioClip(MediaItem m, SourceClip? clip, MediaTime start)
    {
        clip ??= m.Clips.FirstOrDefault();
        return new AudioClip
        {
            MediaId = m.Id,
            SourceClipId = clip?.Id ?? Guid.Empty,
            In = clip?.Start ?? MediaTime.Zero,
            Out = clip?.End ?? m.Duration,
            Start = start,
        };
    }

    public static MediaTime FreeStart(IReadOnlyList<AudioClip> track, MediaTime wanted, MediaTime length, Guid ignore = default)
    {
        MediaTime t = MediaTime.Max(MediaTime.Zero, wanted);
        foreach (AudioClip a in track.Where(a => a.Id != ignore).OrderBy(a => a.Start))
        {
            if (a.End <= t)
            {
                continue;
            }

            if (a.Start >= t + length)
            {
                break;
            }

            t = a.End;
        }

        return t;
    }

    public bool AddAudioClip(MediaItem m, SourceClip? clip, MediaTime start)
    {
        AudioClip a = NewAudioClip(m, clip, start);
        a = a with { Start = FreeStart(Project.AudioMusicTrack, start, a.Length) };
        return Run(UndoNames.AddClip, p =>
        {
            p.AudioMusicTrack.Add(a);
            SortAudio(p);
        });
    }

    public bool AddNarration(MediaItem m, MediaTime start, bool limitToFreeSpace)
    {
        AudioClip a = NewAudioClip(m, null, start) with { Start = start };
        AudioClip? next = Project.AudioMusicTrack.Where(x => x.Start >= start).MinBy(x => x.Start);
        MediaTime shift = MediaTime.Zero;
        if (next is not null && a.End > next.Start)
        {
            if (limitToFreeSpace)
            {
                a = a with { Out = a.In + (next.Start - start) };
            }
            else
            {
                shift = a.End - next.Start;
            }
        }

        if (a.Length <= MediaTime.Zero)
        {
            return false;
        }

        return Run(UndoNames.AddClip, p =>
        {
            if (p.Media.All(x => x.Id != m.Id))
            {
                p.Media.Add(m);
            }

            if (shift > MediaTime.Zero)
            {
                for (int i = 0; i < p.AudioMusicTrack.Count; i++)
                {
                    if (p.AudioMusicTrack[i].Start >= start)
                    {
                        p.AudioMusicTrack[i] = p.AudioMusicTrack[i] with { Start = p.AudioMusicTrack[i].Start + shift };
                    }
                }
            }

            p.AudioMusicTrack.Add(a);
            SortAudio(p);
        });
    }

    public bool MoveAudioClip(Guid id, MediaTime start, string? mergeKey = null)
    {
        AudioClip? a = Project.AudioMusicTrack.FirstOrDefault(x => x.Id == id);
        if (a is null)
        {
            return false;
        }

        start = FreeStart(Project.AudioMusicTrack, start, a.Length, id);
        if (start == a.Start)
        {
            return false;
        }

        return Run(UndoNames.MoveClip, p =>
        {
            int j = p.AudioMusicTrack.FindIndex(x => x.Id == id);
            p.AudioMusicTrack[j] = a with { Start = start };
            SortAudio(p);
        }, mergeKey);
    }

    public bool TrimAudioClip(Guid id, MediaTime newIn, MediaTime newOut, string? mergeKey = null)
    {
        AudioClip? a = Project.AudioMusicTrack.FirstOrDefault(x => x.Id == id);
        MediaItem? m = a is null ? null : Project.FindMedia(a.MediaId);
        if (a is null || m is null)
        {
            return false;
        }

        SourceClip? sc = m.Clip(a.SourceClipId);
        MediaTime lo = sc?.Start ?? MediaTime.Zero, hi = sc?.End ?? m.Duration;
        newIn = newIn.Clamp(lo, hi - MinimumLength);
        newOut = newOut.Clamp(newIn + MinimumLength, hi);
        MediaTime start = a.Start + (newIn - a.In);
        MediaTime prevEnd = Project.AudioMusicTrack.Where(x => x.Id != id && x.End <= a.Start).Select(x => x.End).DefaultIfEmpty(MediaTime.Zero).Max();
        MediaTime nextStart = Project.AudioMusicTrack.Where(x => x.Id != id && x.Start >= a.End).Select(x => x.Start).DefaultIfEmpty(MediaTime.MaxValue).Min();
        if (start < prevEnd)
        {
            newIn += prevEnd - start;
            start = prevEnd;
        }

        if (start + (newOut - newIn) > nextStart)
        {
            newOut = newIn + (nextStart - start);
        }

        return Run(UndoNames.TrimClip, p =>
        {
            int j = p.AudioMusicTrack.FindIndex(x => x.Id == id);
            p.AudioMusicTrack[j] = a with { In = newIn, Out = newOut, Start = start };
        }, mergeKey);
    }

    public bool Nudge(Guid id, MediaTime step)
    {
        MediaTime frame = MediaTime.FrameDuration(Rate);
        int i = Project.IndexOfVideo(id);
        if (i >= 0)
        {
            if (i == 0)
            {
                return false;
            }

            TimelineLayout layout = TimelineLayout.Compute(Project);
            MediaTime overlap = (Project.VideoTrack[i].TransitionIn?.Duration ?? MediaTime.Zero) - step;
            MediaTime start = layout.End(i - 1) - overlap;
            bool fits = overlap >= frame && start >= layout.Starts[i - 1] + frame && start + layout.Lengths[i] >= layout.End(i - 1) + frame;
            if (!fits)
            {
                return false;
            }

            string transition = Project.VideoTrack[i].TransitionIn?.TransitionId ?? TransitionCatalog.FallbackId;
            return UpdateVideo(id, UndoNames.Nudge, c => c with { TransitionIn = new TransitionRef(transition, overlap) }, "nudge:" + id);
        }

        if (Project.AudioMusicTrack.FirstOrDefault(x => x.Id == id) is { } a)
        {
            MediaTime start = a.Start + step;
            if (start < MediaTime.Zero || Project.AudioMusicTrack.Any(x => x.Id != id && x.Start < start + a.Length && x.End > start))
            {
                return false;
            }

            return Run(UndoNames.Nudge, p => p.AudioMusicTrack[p.AudioMusicTrack.FindIndex(x => x.Id == id)] = a with { Start = start }, "nudge:" + id);
        }

        if (Project.TitleOverlayTrack.FirstOrDefault(x => x.Id == id) is { } t)
        {
            MediaTime start = t.Start + step;
            if (start < MediaTime.Zero || Project.TitleOverlayTrack.Any(x => x.Id != id && x.Start < start + t.Duration && x.End > start))
            {
                return false;
            }

            return Run(UndoNames.Nudge, p => p.TitleOverlayTrack[p.TitleOverlayTrack.FindIndex(x => x.Id == id)] = t with { Start = start }, "nudge:" + id);
        }

        return false;
    }

    public bool Nudge(Guid id, int frames) => Nudge(id, MediaTime.FrameDuration(Rate) * frames);

    private static void SortAudio(Project p) => p.AudioMusicTrack.Sort((x, y) => x.Start.CompareTo(y.Start));

    private static void ResolveAudioOverlaps(Project p, Guid changed)
    {
        int j = p.AudioMusicTrack.FindIndex(x => x.Id == changed);
        AudioClip a = p.AudioMusicTrack[j];
        MediaTime start = FreeStart(p.AudioMusicTrack, a.Start, a.Length, changed);
        p.AudioMusicTrack[j] = a with { Start = start };
        SortAudio(p);
    }

    public bool SetAudioLevels(double levels, string? mergeKey = "levels")
    {
        levels = Math.Clamp(levels, -1, 1);
        if (Math.Abs(levels - Project.AudioLevels) < 1e-9)
        {
            return false;
        }

        return Run(UndoNames.TrackVolume, p => p.AudioLevels = levels, mergeKey);
    }

    public VideoClip NewTitleClip(TitleContent content) => new()
    {
        Kind = VideoClipKind.Title,
        Title = content,
        StillDuration = MediaTime.FromSeconds(TitleAnimationCatalog.DefaultDuration(content)).SnapToFrame(Rate),
    };

    public bool AddTitle(TitleContent content, Guid? selectedClip, MediaTime playhead)
    {
        switch (content.Placement)
        {
            case TitlePlacement.AtBeginning:
                return InsertVideoClips(0, [NewTitleClip(content)], UndoNames.AddTitle);
            case TitlePlacement.CreditsAtEnd:
                return InsertVideoClips(Project.VideoTrack.Count, [NewTitleClip(content)], UndoNames.AddTitle);
            case TitlePlacement.BeforeClip:
            case TitlePlacement.AfterClip:
            {
                int i = selectedClip is { } s ? Project.IndexOfVideo(s) : -1;
                if (i < 0)
                {
                    return false;
                }

                return InsertVideoClips(content.Placement == TitlePlacement.BeforeClip ? i : i + 1, [NewTitleClip(content)], UndoNames.AddTitle);
            }

            default:
            {
                MediaTime start = playhead;
                if (selectedClip is { } s && Project.IndexOfVideo(s) is var i and >= 0)
                {
                    start = TimelineLayout.Compute(Project).Starts[i];
                }

                MediaTime d = MediaTime.FromSeconds(TitleAnimationCatalog.DefaultDuration(content)).SnapToFrame(Rate);
                start = FreeTitleStart(start, d);
                var clip = new TitleClip { Start = start, Duration = d, Content = content };
                return Run(UndoNames.AddTitle, p =>
                {
                    p.TitleOverlayTrack.Add(clip);
                    p.TitleOverlayTrack.Sort((x, y) => x.Start.CompareTo(y.Start));
                });
            }
        }
    }

    private MediaTime FreeTitleStart(MediaTime wanted, MediaTime length)
    {
        MediaTime t = wanted;
        foreach (TitleClip c in Project.TitleOverlayTrack.OrderBy(c => c.Start))
        {
            if (c.End <= t)
            {
                continue;
            }

            if (c.Start >= t + length)
            {
                break;
            }

            t = c.End;
        }

        return t;
    }

    public bool EditTitle(Guid id, TitleContent content)
    {
        if (Project.IndexOfVideo(id) >= 0)
        {
            return UpdateVideo(id, UndoNames.ChangeTitle, c => c.Kind == VideoClipKind.Title ? c with { Title = content } : null);
        }

        TitleClip? t = Project.TitleOverlayTrack.FirstOrDefault(x => x.Id == id);
        if (t is null)
        {
            return false;
        }

        return Run(UndoNames.ChangeTitle, p => p.TitleOverlayTrack[p.TitleOverlayTrack.FindIndex(x => x.Id == id)] = t with { Content = content });
    }

    public bool MoveTitleClip(Guid id, MediaTime start, string? mergeKey = null)
    {
        TitleClip? t = Project.TitleOverlayTrack.FirstOrDefault(x => x.Id == id);
        if (t is null)
        {
            return false;
        }

        start = MediaTime.Max(MediaTime.Zero, start);
        if (Project.TitleOverlayTrack.Any(x => x.Id != id && x.Start < start + t.Duration && x.End > start))
        {
            return false;
        }

        return Run(UndoNames.MoveClip, p =>
        {
            p.TitleOverlayTrack[p.TitleOverlayTrack.FindIndex(x => x.Id == id)] = t with { Start = start };
            p.TitleOverlayTrack.Sort((x, y) => x.Start.CompareTo(y.Start));
        }, mergeKey);
    }

    public bool MoveTitleToVideo(Guid id, int index)
    {
        TitleClip? t = Project.TitleOverlayTrack.FirstOrDefault(x => x.Id == id);
        if (t is null)
        {
            return false;
        }

        TitleContent content = t.Content.Placement == TitlePlacement.OnClip ? t.Content with { Placement = TitlePlacement.BeforeClip } : t.Content;
        var clip = new VideoClip { Id = t.Id, Kind = VideoClipKind.Title, Title = content, StillDuration = t.Duration };
        return Run(UndoNames.MoveClip, p =>
        {
            p.TitleOverlayTrack.RemoveAll(x => x.Id == id);
            p.VideoTrack.Insert(Math.Clamp(index, 0, p.VideoTrack.Count), clip);
            if (p.VideoTrack[0].TransitionIn is not null)
            {
                p.VideoTrack[0] = p.VideoTrack[0] with { TransitionIn = null };
            }
        });
    }

    public bool MoveTitleToOverlay(Guid id, MediaTime start, bool copy, out Guid placed)
    {
        placed = Guid.Empty;
        int i = Project.IndexOfVideo(id);
        if (i < 0 || Project.VideoTrack[i] is not { Kind: VideoClipKind.Title, Title: { } content } clip)
        {
            return false;
        }

        start = MediaTime.Max(MediaTime.Zero, start).SnapToFrame(Rate);
        if (Project.TitleOverlayTrack.Any(x => x.Start < start + clip.StillDuration && x.End > start))
        {
            return false;
        }

        var title = new TitleClip { Id = copy ? Guid.NewGuid() : clip.Id, Start = start, Duration = clip.StillDuration, Content = content with { Placement = TitlePlacement.OnClip } };
        placed = title.Id;
        return Run(copy ? UndoNames.AddClip : UndoNames.MoveClip, p =>
        {
            if (!copy)
            {
                p.VideoTrack.RemoveAt(i);
                if (p.VideoTrack.Count > 0 && p.VideoTrack[0].TransitionIn is not null)
                {
                    p.VideoTrack[0] = p.VideoTrack[0] with { TransitionIn = null };
                }
            }

            p.TitleOverlayTrack.Add(title);
            p.TitleOverlayTrack.Sort((x, y) => x.Start.CompareTo(y.Start));
        });
    }

    public bool SetTitleClipDuration(Guid id, MediaTime duration, string? mergeKey = null)
    {
        TitleClip? t = Project.TitleOverlayTrack.FirstOrDefault(x => x.Id == id);
        if (t is null)
        {
            return false;
        }

        duration = MediaTime.Max(MinimumLength, duration.SnapToFrame(Rate));
        MediaTime next = Project.TitleOverlayTrack.Where(x => x.Id != id && x.Start >= t.Start).Select(x => x.Start).DefaultIfEmpty(MediaTime.MaxValue).Min();
        if (t.Start + duration > next)
        {
            duration = next - t.Start;
        }

        return Run(UndoNames.TrimClip, p => p.TitleOverlayTrack[p.TitleOverlayTrack.FindIndex(x => x.Id == id)] = t with { Duration = duration }, mergeKey);
    }

    public bool ClearTimeline(string? name = null)
    {
        if (Project.IsEmpty)
        {
            return false;
        }

        return Run(name ?? UndoNames.ClearTimeline, p =>
        {
            p.VideoTrack.Clear();
            p.AudioMusicTrack.Clear();
            p.TitleOverlayTrack.Clear();
        });
    }

    public bool SetProperties(ProjectProperties properties)
    {
        if (properties == Project.Properties)
        {
            return false;
        }

        return Run(UndoNames.SetProperty, p => p.Properties = properties);
    }

    public bool Batch(string name, Action<Project> change) => Run(name, change);

    public MediaTime Snap(MediaTime t, MediaTime tolerance, MediaTime playhead, Guid ignore = default)
    {
        var targets = new List<MediaTime> { MediaTime.Zero, playhead };
        TimelineLayout layout = TimelineLayout.Compute(Project);
        for (int i = 0; i < layout.Count; i++)
        {
            if (Project.VideoTrack[i].Id == ignore)
            {
                continue;
            }

            targets.Add(layout.Starts[i]);
            targets.Add(layout.End(i));
        }

        foreach (AudioClip a in Project.AudioMusicTrack.Where(a => a.Id != ignore))
        {
            targets.Add(a.Start);
            targets.Add(a.End);
        }

        foreach (TitleClip c in Project.TitleOverlayTrack.Where(c => c.Id != ignore))
        {
            targets.Add(c.Start);
            targets.Add(c.End);
        }

        MediaTime best = t;
        long bestDist = tolerance.Ticks + 1;
        foreach (MediaTime target in targets)
        {
            long d = Math.Abs((target - t).Ticks);
            if (d < bestDist)
            {
                bestDist = d;
                best = target;
            }
        }

        return bestDist <= tolerance.Ticks ? best : t;
    }

    private Rational SourceRate(Guid mediaId)
    {
        MediaItem? m = Project.FindMedia(mediaId);
        return m?.Video is { } v && v.FrameRateNum > 0 && v.FrameRateDen > 0 ? new Rational(v.FrameRateNum, v.FrameRateDen) : Rate;
    }
}
