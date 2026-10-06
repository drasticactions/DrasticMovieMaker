using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Media;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Interop.Mswmm;

internal static class MswmmReader
{
    private const string ProducerStream = "ProducerData/Producer.Dat";
    private const int TrackVideo = 0;
    private const int TrackAudio = 1;
    private const int TrackTitleOverlay = 5;
    private const int MaxSupportedFileVersion = 4;
    private const string TfxPrefix = "TFX\\";

    public static Project Read(CompoundFile cfb, List<string> warnings)
    {
        CompoundFile.Entry? entry = cfb.Find(ProducerStream);
        if (entry is null || !entry.IsStream)
        {
            throw new InvalidDataException(Strings.NoTimelineData);
        }

        XDocument doc = ParseXml(cfb.Read(entry));
        return new Builder(doc, warnings).Build();
    }

    internal static XDocument ParseXml(byte[] data)
    {
        string text;
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(data, 2, data.Length - 2);
        }
        else if (data.Length >= 2 && data[1] == 0 && data[0] != 0)
        {
            text = Encoding.Unicode.GetString(data, 0, data.Length & ~1);
        }
        else
        {
            text = Encoding.UTF8.GetString(data);
        }

        text = text.TrimEnd('\0', '\r', '\n', ' ');
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = true };
        try
        {
            using var sr = new StringReader(text);
            using var xr = XmlReader.Create(sr, settings);
            return XDocument.Load(xr);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, Strings.TimelineDataDamaged, ex.Message), ex);
        }
    }

    private sealed class Builder(XDocument doc, List<string> warnings)
    {
        private readonly Dictionary<int, XElement> _objects = [];
        private readonly Dictionary<int, (string Path, bool Relative)> _files = [];
        private readonly Dictionary<int, MediaBuild> _media = [];
        private readonly List<MediaBuild> _mediaOrder = [];
        private readonly HashSet<string> _warned = [];

        public Project Build()
        {
            XElement root = doc.Root ?? throw new InvalidDataException(Strings.TimelineDataEmpty);
            if (root.Name.LocalName != "MovieMaker")
            {
                throw new InvalidDataException(Strings.TimelineDataNotAProject);
            }

            if (Int(root, "FileVer", 0) > MaxSupportedFileVersion)
            {
                Warn(Strings.WarnNewerVersion, Int(root, "FileVer", 0));
            }

            XElement project = root.Element("Project") ?? throw new InvalidDataException(Strings.TimelineDataNoProject);
            XElement data = project.Element("DataStr") ?? throw new InvalidDataException(Strings.TimelineDataNoData);
            foreach (XElement e in data.Elements())
            {
                int uid = Int(e, "UID", 0);
                if (uid > 0)
                {
                    _objects.TryAdd(uid, e);
                }

                if (e.Name.LocalName == "FileInfo")
                {
                    int id = Int(e, "FileID", 0);
                    string path = (string?)e.Attribute("SrceFn") ?? string.Empty;
                    if (id > 0 && path.Length > 0)
                    {
                        _files.TryAdd(id, (path, Int(e, "FileRelative", 0) != 0));
                    }
                }
            }

            var result = new Project();
            XElement? props = data.Elements("ProducerProperties").FirstOrDefault();
            if (props is not null)
            {
                ReadProperties(props, result);
            }

            double videoVolume = 1, musicVolume = 1;
            foreach (XElement track in Tracks(project, data))
            {
                int type = Int(track, "TrackTyp", -1);
                if (type == TrackVideo)
                {
                    videoVolume = Math.Clamp(Dbl(track, "TrkVolume", 1), 0, 1);
                }
                else if (type == TrackAudio)
                {
                    musicVolume = Math.Clamp(Dbl(track, "TrkVolume", 1), 0, 1);
                }

                List<XElement> items = Items(Ref(track, "TrkClips"), "track clips");
                switch (type)
                {
                    case TrackVideo:
                        ReadVideoTrack(items, result);
                        break;
                    case TrackAudio:
                        ReadAudioTrack(items, result);
                        break;
                    case TrackTitleOverlay:
                        ReadTitleOverlayTrack(items, result);
                        break;
                    default:
                        if (items.Count > 0)
                        {
                            Warn(Strings.WarnUnknownTrack, type, items.Count);
                        }

                        break;
                }
            }

            result.AudioLevels = videoVolume < 1 ? 1 - videoVolume : musicVolume - 1;
            result.Media = _mediaOrder.Select(m => m.ToMediaItem()).ToList();
            result.AudioMusicTrack.Sort((a, b) => a.Start.CompareTo(b.Start));
            return result;
        }

        private void ReadProperties(XElement props, Project result)
        {
            int ax = Int(props, "ProjectAspectRatioX", 4);
            int ay = Int(props, "ProjectAspectRatioY", 3);
            AspectRatio aspect = ax * 9 == ay * 16 ? AspectRatio.Widescreen16x9 : AspectRatio.Standard4x3;
            if (aspect == AspectRatio.Standard4x3 && ax * 3 != ay * 4)
            {
                Warn(Strings.WarnUnknownAspect, ax, ay);
            }

            result.Settings = result.Settings with { Aspect = aspect };
            ProjectProperties p = result.Properties;
            foreach (XElement md in props.Elements("MetDat"))
            {
                string tag = (string?)md.Attribute("MDTag") ?? string.Empty;
                string val = (string?)md.Attribute("MDVal") ?? string.Empty;
                p = tag switch
                {
                    "PresentationTitle" or "Title" => p with { Title = val },
                    "Author" => p with { Author = val },
                    "Copyright" => p with { Copyright = val },
                    "Rating" or "ParentalRating" => p with { Rating = val },
                    "Description" => p with { Description = val },
                    _ => p,
                };
            }

            result.Properties = p;
        }

        private List<XElement> Tracks(XElement project, XElement data)
        {
            var tracks = new List<XElement>();
            XElement? strmArr = project.Element("Timeline")?.Element("StrmArr");
            if (strmArr is not null)
            {
                foreach (XElement stream in Items(Int(strmArr, "UID", 0), "streams"))
                {
                    foreach (XElement t in Items(Ref(stream, "StrmTrks"), "tracks"))
                    {
                        if (t.Name.LocalName == "Track")
                        {
                            tracks.Add(t);
                        }
                    }
                }
            }

            if (tracks.Count == 0)
            {
                tracks.AddRange(data.Elements("Track"));
            }

            return tracks;
        }

        private void ReadVideoTrack(List<XElement> items, Project result)
        {
            var ordered = items.Select((e, i) => (e, i)).OrderBy(x => Dbl(x.e, "TmlnSrt", 0)).ThenBy(x => x.i).Select(x => x.e).ToList();
            XElement? previous = null;
            foreach (XElement item in ordered)
            {
                string kind = item.Name.LocalName;
                VideoClip? clip = kind switch
                {
                    "TmlnVideoItem" or "TmlnAVItem" or "TmlnVideoOnlyItem" => VideoItem(item),
                    "TmlnStillItem" => StillItem(item),
                    "TiTitleSource" => TitleItem(item),
                    _ => null,
                };
                if (clip is null)
                {
                    if (kind is not ("TmlnVideoItem" or "TmlnAVItem" or "TmlnVideoOnlyItem" or "TmlnStillItem" or "TiTitleSource"))
                    {
                        Warn(Strings.WarnVideoItemType, kind);
                    }

                    continue;
                }

                clip = clip with { Effects = Effects(item) };
                if (Math.Abs(Dbl(item, "ClipSpeed", 1) - clip.Speed) > 1e-6)
                {
                    Warn(Strings.WarnClipSpeed, Dbl(item, "ClipSpeed", 1).ToString(CultureInfo.InvariantCulture));
                }

                if (previous is not null && result.VideoTrack.Count > 0)
                {
                    clip = clip with { TransitionIn = Transition(item, previous) };
                }

                result.VideoTrack.Add(clip);
                previous = item;
            }

            List<VideoClip> track = result.VideoTrack;
            for (int i = 0; i < track.Count; i++)
            {
                if (track[i] is { Kind: VideoClipKind.Title, Title: { } t })
                {
                    bool last = i == track.Count - 1;
                    TitlePlacement placement = i == 0 ? TitlePlacement.AtBeginning
                        : last && IsCredits(t.AnimationId) ? TitlePlacement.CreditsAtEnd
                        : last ? TitlePlacement.AfterClip
                        : TitlePlacement.BeforeClip;
                    track[i] = track[i] with { Title = t with { Placement = placement } };
                }
            }
        }

        private static bool IsCredits(string animationId) => TitleAnimationCatalog.Find(animationId)?.IsCredits == true;

        private VideoClip? TitleItem(XElement item)
        {
            TitleContent? content = TitleOf(item, TitlePlacement.BeforeClip);
            if (content is null)
            {
                return null;
            }

            MediaTime length = Time(item, "TmlnEnd") - Time(item, "TmlnSrt");
            if (length <= MediaTime.Zero)
            {
                Warn(Strings.WarnTitleNoDuration, content.Summary);
                return null;
            }

            return new VideoClip { Kind = VideoClipKind.Title, StillDuration = length, Title = content };
        }

        private void ReadTitleOverlayTrack(List<XElement> items, Project result)
        {
            foreach (XElement item in items)
            {
                if (item.Name.LocalName != "TiTitleOverlay")
                {
                    Warn(Strings.WarnOverlayItemType, item.Name.LocalName);
                    continue;
                }

                TitleContent? content = TitleOf(item, TitlePlacement.OnClip);
                MediaTime start = Time(item, "TmlnSrt");
                MediaTime length = Time(item, "TmlnEnd") - start;
                if (content is null || length <= MediaTime.Zero)
                {
                    if (content is not null)
                    {
                        Warn(Strings.WarnOverlayNoDuration, content.Summary);
                    }

                    continue;
                }

                if (Ref(item, "TiEffectArr") != 0)
                {
                    Warn(Strings.WarnOverlayEffects);
                }

                result.TitleOverlayTrack.Add(new TitleClip { Start = start, Duration = length, Content = content });
            }

            result.TitleOverlayTrack.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        private TitleContent? TitleOf(XElement item, TitlePlacement placement)
        {
            XElement? ptr = item.Element("TiEffectPtr");
            if (ptr is null)
            {
                Warn(Strings.WarnTitleNoSettings, item.Name.LocalName, Int(item, "UID", 0));
                return null;
            }

            string guid = (string?)ptr.Attribute("TFXGuid") ?? string.Empty;
            string name = (string?)ptr.Attribute("TFXName") ?? string.Empty;
            string tfxId = (string?)ptr.Attribute("TFXID") ?? string.Empty;
            Dictionary<string, string> ps = TitleParams(ptr, out List<(int Para, int Block, string Text)> texts);
            string? animation = MswmmIdMap.TitleAnimation(StripTfx(guid))
                ?? (name.Length > 0 ? MswmmIdMap.TitleAnimation(name) : null)
                ?? (tfxId.Length > 0 ? MswmmIdMap.TitleAnimation(tfxId) : null);
            bool creditsLike = ps.TryGetValue("Animation", out string? anim) && anim.StartsWith("Credit", StringComparison.OrdinalIgnoreCase);
            if (animation is null)
            {
                animation = creditsLike ? TitleAnimationCatalog.DefaultCredits : TitleAnimationCatalog.DefaultTitle;
                Warn(Strings.WarnTitleAnimation, name.Length > 0 ? name : guid, animation);
            }

            var content = new TitleContent { Placement = placement, AnimationId = animation };
            if (IsCredits(animation))
            {
                content = content with { Credits = CreditRows(texts) };
            }
            else
            {
                var lines = texts.Where(t => t.Block < 0).Select(t => t.Text).ToList();
                while (lines.Count > 0 && lines[^1].Length == 0)
                {
                    lines.RemoveAt(lines.Count - 1);
                }

                if (texts.Any(t => t.Block >= 0))
                {
                    Warn(Strings.WarnTitleTextBlocks);
                }

                content = content with { Lines = lines };
            }

            var font = new TitleFont
            {
                Family = ps.TryGetValue("Font", out string? family) && family.Trim().Length > 0 ? family.Trim() : new TitleFont().Family,
                Bold = Bool(ps, "IsBold"),
                Italic = Bool(ps, "IsItalic"),
                Underline = Bool(ps, "IsUnderline"),
                SizeStep = SizeStep(ps),
            };
            content = content with { Font = font };
            if (ps.TryGetValue("FrontColor1", out string? front) && Color(front) is uint fc)
            {
                uint alpha = fc >> 24;
                content = content with
                {
                    TextColor = fc | 0xFF000000,
                    Transparency = (int)Math.Round((255 - alpha) * 100 / 255.0),
                };
            }

            if (ps.TryGetValue("BackgroundVideoSourceColor", out string? back) && Color(back) is uint bc)
            {
                content = content with { BackgroundColor = bc | 0xFF000000 };
            }

            if (ps.TryGetValue("BannerColor1", out string? banner) && Color(banner) is uint bn)
            {
                content = content with { BannerColor = bn };
            }

            if (ps.TryGetValue("HorizontalAlignment", out string? align))
            {
                content = content with
                {
                    Alignment = align.Trim().ToLowerInvariant() switch
                    {
                        "left" => TitleAlignment.Left,
                        "right" => TitleAlignment.Right,
                        _ => TitleAlignment.Center,
                    },
                };
            }

            return content;
        }

        private static List<CreditRow> CreditRows(List<(int Para, int Block, string Text)> texts)
        {
            var rows = new List<CreditRow>();
            var paragraphs = texts.GroupBy(t => t.Para).Select(g => g.ToList()).ToList();
            for (int i = 0; i < paragraphs.Count; i++)
            {
                List<(int Para, int Block, string Text)> para = paragraphs[i];
                if (para.All(t => t.Block < 0))
                {
                    rows.AddRange(para.Select(t => new CreditRow(t.Text, string.Empty)));
                    continue;
                }

                List<(int Para, int Block, string Text)>? names = i + 1 < paragraphs.Count && paragraphs[i + 1].All(t => t.Block >= 0) ? paragraphs[++i] : null;
                string[] headings = [.. para.OrderBy(t => t.Block).Select(t => t.Text)];
                string[] named = names is null ? [] : [.. names.OrderBy(t => t.Block).Select(t => t.Text)];
                for (int c = 0; c < Math.Max(headings.Length, named.Length); c++)
                {
                    rows.Add(new CreditRow(c < headings.Length ? headings[c] : string.Empty, c < named.Length ? named[c] : string.Empty));
                }
            }

            rows.RemoveAll(r => r.Heading.Length == 0 && r.Names.Length == 0);
            return rows;
        }

        private static Dictionary<string, string> TitleParams(XElement ptr, out List<(int Para, int Block, string Text)> texts)
        {
            var ps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            texts = [];
            foreach (XElement p in ptr.Elements("FXParamList"))
            {
                string key = (string?)p.Attribute("FXParamName") ?? string.Empty;
                string value = (string?)p.Attribute("FXParamValue") ?? string.Empty;
                int paren = key.IndexOf('(');
                if (paren >= 0)
                {
                    key = key[..paren];
                }

                string[] parts = key.Split('/');
                if (parts.Length >= 2 && parts[^1] == "Text" && Index(parts[0], "Paragraph") is int para)
                {
                    int block = parts.Length == 3 && Index(parts[1], "TextBlock") is int b ? b : -1;
                    texts.Add((para, block, value));
                    continue;
                }

                ps.TryAdd(key, value);
            }

            texts.Sort((a, b) => a.Para != b.Para ? a.Para.CompareTo(b.Para) : a.Block.CompareTo(b.Block));
            return ps;
        }

        private static int? Index(string part, string name)
        {
            int colon = part.IndexOf(':');
            return colon > 0 && part[(colon + 1)..] == name && int.TryParse(part[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                ? n : null;
        }

        private static bool Bool(Dictionary<string, string> ps, string key) =>
            ps.TryGetValue(key, out string? v) && (v.Trim() == "1" || v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));

        private static int SizeStep(Dictionary<string, string> ps)
        {
            if (!ps.TryGetValue("FontSizeMultiplier", out string? v)
                || !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double m) || !(m > 0) || !double.IsFinite(m))
            {
                return 0;
            }

            return Math.Clamp((int)Math.Round(Math.Log(m) / Math.Log(TitleLayout.StepFactor)), -6, 10);
        }

        private static uint? Color(string text)
        {
            string hex = text.Trim().TrimStart('#');
            if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            {
                return null;
            }

            return hex.Length == 6 ? v | 0xFF000000 : v;
        }

        private void ReadAudioTrack(List<XElement> items, Project result)
        {
            foreach (XElement item in items)
            {
                if (item.Name.LocalName is not ("TmlnAudioItem" or "TmlnVideoItem" or "TmlnAVItem"))
                {
                    Warn(Strings.WarnAudioItemType, item.Name.LocalName);
                    continue;
                }

                if (!Source(item, out MediaBuild? media, out SourceClip? sc))
                {
                    continue;
                }

                result.AudioMusicTrack.Add(new AudioClip
                {
                    MediaId = media.Id,
                    SourceClipId = sc.Id,
                    In = Time(item, "ClpSrt"),
                    Out = Time(item, "ClpEnd"),
                    RangeIn = OriginalTime(item, "OrgClpSrt"),
                    RangeOut = OriginalTime(item, "OrgClpEnd"),
                    Start = Time(item, "TmlnSrt"),
                    Audio = AudioOf(item),
                });
                if (Ref(item, "TiEffectArr") != 0)
                {
                    Warn(Strings.WarnAudioEffects);
                }
            }
        }

        private VideoClip? VideoItem(XElement item)
        {
            if (!Source(item, out MediaBuild? media, out SourceClip? sc))
            {
                return null;
            }

            MediaTime inPoint = Time(item, "ClpSrt");
            MediaTime outPoint = Time(item, "ClpEnd");
            if (outPoint <= inPoint)
            {
                Warn(Strings.WarnEmptyTrim, sc.Name);
                return null;
            }

            return new VideoClip
            {
                Kind = VideoClipKind.Video,
                MediaId = media.Id,
                SourceClipId = sc.Id,
                In = inPoint,
                Out = outPoint,
                RangeIn = OriginalTime(item, "OrgClpSrt"),
                RangeOut = OriginalTime(item, "OrgClpEnd"),
                Audio = AudioOf(item),
            };
        }

        private static MediaTime? OriginalTime(XElement item, string name) => item.Attribute(name) is null ? null : Time(item, name);

        private VideoClip? StillItem(XElement item)
        {
            if (!Source(item, out MediaBuild? media, out SourceClip? sc))
            {
                return null;
            }

            MediaTime length = Time(item, "ClpEnd") - Time(item, "ClpSrt");
            if (length <= MediaTime.Zero)
            {
                length = Time(item, "TmlnEnd") - Time(item, "TmlnSrt");
            }

            if (length <= MediaTime.Zero)
            {
                length = MediaTime.FromSeconds(5);
                Warn(Strings.WarnPictureNoDuration, sc.Name);
            }

            return new VideoClip { Kind = VideoClipKind.Picture, MediaId = media.Id, SourceClipId = sc.Id, StillDuration = length };
        }

        private static AudioSettings AudioOf(XElement item) => new()
        {
            Volume = Math.Clamp(Dbl(item, "ClipVolume", 1), 0, AudioSettings.MaxVolume),
            Mute = Int(item, "TmlnMute", 0) != 0,
            FadeIn = Int(item, "TmlnFadeIn", 0) != 0,
            FadeOut = Int(item, "TmlnFadeOut", 0) != 0,
        };

        private bool Source(XElement item, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MediaBuild? media,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SourceClip? clip)
        {
            media = null;
            clip = null;
            XElement? wm = Get(Ref(item, "ClipWMItem"));
            XElement? src = wm is null ? null : Get(Ref(wm, "Srce"));
            if (wm is null || src is null || src.Name.LocalName != "AVSource")
            {
                Warn(Strings.WarnNoSourceFile, item.Name.LocalName, Int(item, "UID", 0));
                return false;
            }

            int srcUid = Int(src, "UID", 0);
            if (!_media.TryGetValue(srcUid, out media))
            {
                int fileId = Int(src, "FileID", 0);
                if (!_files.TryGetValue(fileId, out var file))
                {
                    Warn(Strings.WarnSourceNoPath, fileId);
                    return false;
                }

                media = MediaBuild.From(src, file.Path, file.Relative, (string?)wm.Attribute("ClpNam"));
                _media.Add(srcUid, media);
                _mediaOrder.Add(media);
            }

            clip = media.ClipFor((string?)wm.Attribute("ClpNam"), Time(wm, "ClpSrt"), Time(wm, "ClpEnd"));
            return true;
        }

        private List<EffectRef> Effects(XElement item)
        {
            var list = new List<EffectRef>();
            foreach (XElement fx in Items(Ref(item, "TiEffectArr"), "effects"))
            {
                XElement? ptr = fx.Element("TiEffectPtr");
                if (fx.Name.LocalName != "TiEffect" || ptr is null)
                {
                    Warn(Strings.WarnEffectRecord, fx.Name.LocalName);
                    continue;
                }

                string guid = (string?)ptr.Attribute("TFXGuid") ?? string.Empty;
                string name = (string?)ptr.Attribute("TFXName") ?? string.Empty;
                string? id = MswmmIdMap.Effect(StripTfx(guid)) ?? MswmmIdMap.EffectByName(name);
                if (id is null)
                {
                    Warn(Strings.WarnEffectUnavailable, name.Length > 0 ? name : guid);
                    continue;
                }

                list.Add(new EffectRef(id));
            }

            return list;
        }

        private TransitionRef? Transition(XElement item, XElement previous)
        {
            XElement? tr = Get(Ref(item, "TAVTransition"));
            if (tr is null)
            {
                return null;
            }

            XElement? ptr = tr.Element("TiTransitionPtr");
            if (tr.Name.LocalName != "TiTransition" || ptr is null)
            {
                Warn(Strings.WarnTransitionRecord, tr.Name.LocalName);
                return null;
            }

            string guid = (string?)ptr.Attribute("TFXGuid") ?? string.Empty;
            string name = (string?)ptr.Attribute("TFXName") ?? string.Empty;
            string? id = MswmmIdMap.Transition(StripTfx(guid)) ?? MswmmIdMap.TransitionByName(name);
            if (id is null)
            {
                Warn(Strings.WarnTransitionUnavailable, name.Length > 0 ? name : guid);
                return null;
            }

            MediaTime overlap = Time(previous, "TmlnEnd") - Time(item, "TmlnSrt");
            if (overlap <= MediaTime.Zero)
            {
                double declared = Dbl(ptr, "TFXDuration", 0);
                overlap = declared > 0 ? MediaTime.FromSeconds(declared) : new ProjectSettings().TransitionDuration;
            }

            return new TransitionRef(id, overlap);
        }

        private static string StripTfx(string guid) =>
            guid.StartsWith(TfxPrefix, StringComparison.OrdinalIgnoreCase) ? guid[TfxPrefix.Length..] : guid;

        private XElement? Get(int uid) => uid > 0 && _objects.TryGetValue(uid, out XElement? e) ? e : null;

        private static int Ref(XElement owner, string name) => owner.Element(name) is { } r ? Int(r, "UID", 0) : 0;

        private List<XElement> Items(int arrayUid, string what)
        {
            var list = new List<XElement>();
            XElement? arr = Get(arrayUid);
            if (arr is null)
            {
                return list;
            }

            foreach (XElement r in arr.Elements("UID"))
            {
                int uid = Int(r, "UID", 0);
                if (Get(uid) is { } target)
                {
                    list.Add(target);
                }
                else
                {
                    Warn(Strings.WarnMissingRecord, what, uid);
                }
            }

            return list;
        }

        private void Warn(string format, params object?[] args) => Warn(string.Format(CultureInfo.CurrentCulture, format, args));

        private void Warn(string message)
        {
            if (_warned.Add(message))
            {
                warnings.Add(message);
            }
        }
    }

    private sealed class MediaBuild
    {
        private readonly List<SourceClip> _clips = [];

        public Guid Id { get; } = Guid.NewGuid();

        public required MediaItem Item { get; init; }

        public static MediaBuild From(XElement src, string path, bool relative, string? clipName)
        {
            bool isVideo = Int(src, "SrcIsVideo", 0) != 0;
            bool isAudio = Int(src, "SrcIsAudio", 0) != 0;
            MediaKind kind = Int(src, "FileKind", 0) switch
            {
                4 => MediaKind.Picture,
                3 => MediaKind.Audio,
                2 => MediaKind.Video,
                _ => isVideo ? (Dbl(src, "SrcDuration", 0) > 0 ? MediaKind.Video : MediaKind.Picture) : MediaKind.Audio,
            };
            string name = MswmmImporter.WindowsFileName(path);
            int dot = name.LastIndexOf('.');
            name = dot > 0 ? name[..dot] : name;
            int width = Int(src, "SrcWidth", 0);
            int height = Int(src, "SrcHeight", 0);
            long arx = Long(src, "SrcVideoARX", 1);
            long ary = Long(src, "SrcVideoARY", 1);
            (long Num, long Den) sar = SampleAspect(arx, ary, width, height);
            DateTime modified = FileTime(Long(src, "SrcModifyHigh", 0), Long(src, "SrcModifyLow", 0));
            DateTime taken = FileTime(Long(src, "DateTakenHigh", 0), Long(src, "DateTakenLow", 0));
            return new MediaBuild
            {
                Item = new MediaItem
                {
                    Kind = kind,
                    Path = path,
                    RelativePath = relative ? path : null,
                    Name = string.IsNullOrEmpty(clipName) ? name : clipName,
                    Duration = kind == MediaKind.Picture ? MediaTime.Zero : MediaTime.FromSeconds(Math.Max(0, Dbl(src, "SrcDuration", 0))),
                    Video = isVideo || kind == MediaKind.Picture
                        ? new VideoProperties { Width = width, Height = height, SampleAspectNum = sar.Num, SampleAspectDen = sar.Den }
                        : null,
                    Audio = isAudio && kind != MediaKind.Picture ? new AudioProperties() : null,
                    FileSize = Math.Max(0, Long(src, "FileSize", 0)) * 1024,
                    LastWriteTimeUtc = modified,
                    DateTaken = taken == default ? null : new DateTimeOffset(taken),
                },
            };
        }

        public SourceClip ClipFor(string? name, MediaTime start, MediaTime end)
        {
            if (Item.Kind == MediaKind.Picture)
            {
                start = end = MediaTime.Zero;
            }
            else if (end <= start)
            {
                start = MediaTime.Zero;
                end = Item.Duration;
            }

            name = string.IsNullOrEmpty(name) ? Item.Name : name;
            SourceClip? found = _clips.FirstOrDefault(c => c.Start == start && c.End == end && c.Name == name);
            if (found is null)
            {
                found = new SourceClip { Name = name, Start = start, End = end };
                _clips.Add(found);
            }

            return found;
        }

        public MediaItem ToMediaItem() => Item with { Id = Id, Clips = [.. _clips] };

        private static (long Num, long Den) SampleAspect(long arx, long ary, int width, int height)
        {
            if (arx <= 0 || ary <= 0 || width <= 0 || height <= 0 || (arx == 1 && ary == 1) || arx * height == ary * width)
            {
                return (1, 1);
            }

            long num = arx * height;
            long den = ary * width;
            long g = Gcd(num, den);
            return (num / g, den / g);
        }

        private static long Gcd(long a, long b)
        {
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }

            return Math.Max(1, a);
        }

        private static DateTime FileTime(long high, long low)
        {
            long ft = (long)(((ulong)(uint)high << 32) | (uint)low);
            if (ft <= 0 || ft > DateTime.MaxValue.ToFileTimeUtc())
            {
                return default;
            }

            return DateTime.FromFileTimeUtc(ft);
        }
    }

    private static MediaTime Time(XElement e, string attr) => MediaTime.FromSeconds(Dbl(e, attr, 0));

    private static double Dbl(XElement e, string attr, double fallback)
    {
        string? s = (string?)e.Attribute(attr);
        return s is not null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v)
            && Math.Abs(v) < 1e7 ? v : fallback;
    }

    private static int Int(XElement e, string attr, int fallback)
    {
        string? s = (string?)e.Attribute(attr);
        return s is not null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
    }

    private static long Long(XElement e, string attr, long fallback)
    {
        string? s = (string?)e.Attribute(attr);
        return s is not null && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : fallback;
    }
}
