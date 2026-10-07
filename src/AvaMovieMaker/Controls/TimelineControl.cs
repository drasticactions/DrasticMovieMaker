using System.Globalization;
using AvaMovieMaker.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using AvaMovieMaker.Converters;
using AvaMovieMaker.Views;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Media.Analysis;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Session;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.Controls;

public sealed class TimelineControl : Control
{
    public const double HeaderWidth = 114;
    public const double RulerHeight = 17;
    private const double EdgeGrab = 5;

    private TimelineViewModel? _vm;
    private Gesture _gesture;
    private Guid _gestureClip;
    private TimelineTrack _gestureTrack;
    private PointerPressedEventArgs? _pressed;
    private Point _pressPoint;
    private (TimelineTrack Track, double X)? _drop;
    private DragPayload? _dropPayload;

    private enum Gesture
    {
        None,
        Seek,
        Trim,
        Move,
        PendingDrag,
        Marquee,
    }

    private (TimelineTrack Track, double From, double To)? _marquee;
    private (TimelineTrack Track, Guid Id)? _collapse;

    public TimelineControl()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        ClipToBounds = true;
        Focusable = true;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, e) =>
        {
            if (!new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            {
                _drop = null;
                _scrollTimer?.Stop();
                InvalidateVisual();
            }
        });
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    public event Action? LayoutChanged;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.Invalidated -= OnInvalidated;
        }

        _vm = DataContext as TimelineViewModel;
        if (_vm is not null)
        {
            _vm.Invalidated += OnInvalidated;
        }

        InvalidateVisual();
    }

    private void OnInvalidated(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            InvalidateVisual();
            LayoutChanged?.Invoke();
        });
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutChanged?.Invoke();
    }

    private readonly record struct Row(TimelineTrack Track, double Top, double Height, string Label);

    private List<Row> Rows()
    {
        double y = RulerHeight;
        var rows = new List<Row>();
        if (_vm?.IsVideoExpanded == true)
        {
            rows.Add(new Row(TimelineTrack.Video, y, 50, Strings.TrackVideo));
            y += 50;
            rows.Add(new Row(TimelineTrack.Transition, y, 21, Strings.TrackTransition));
            y += 21;
            rows.Add(new Row(TimelineTrack.Audio, y, 21, Strings.TrackAudio));
            y += 21;
            rows.Add(new Row(TimelineTrack.AudioMusic, y, 21, Strings.TrackAudioMusic));
            y += 21;
            rows.Add(new Row(TimelineTrack.TitleOverlay, y, 21, Strings.TrackTitleOverlay));
        }
        else
        {
            rows.Add(new Row(TimelineTrack.Video, y, 55, Strings.TrackVideo));
            y += 55;
            rows.Add(new Row(TimelineTrack.AudioMusic, y, 54, Strings.TrackAudioMusic));
            y += 54;
            rows.Add(new Row(TimelineTrack.TitleOverlay, y, 26, Strings.TrackTitleOverlay));
        }

        return rows;
    }

    private double X(MediaTime t) => HeaderWidth + _vm!.ToPixels(t);

    private MediaTime T(double x) => _vm!.ToTime(x - HeaderWidth);

    private static Rect ExpandBox => new(91, RulerHeight + 18, 16, 16);

    private IEnumerable<(Guid Id, MediaTime Start, MediaTime End)> Spans(TimelineTrack track)
    {
        Project p = _vm!.Project;
        switch (track)
        {
            case TimelineTrack.Video:
            case TimelineTrack.Audio:
            {
                TimelineLayout l = TimelineLayout.Compute(p);
                for (int i = 0; i < l.Count; i++)
                {
                    if (track == TimelineTrack.Audio && (p.VideoTrack[i].Kind != VideoClipKind.Video || p.FindMedia(p.VideoTrack[i].MediaId)?.HasAudio != true))
                    {
                        continue;
                    }

                    yield return (p.VideoTrack[i].Id, l.Starts[i], l.End(i));
                }

                break;
            }

            case TimelineTrack.Transition:
            {
                TimelineLayout l = TimelineLayout.Compute(p);
                for (int i = 1; i < l.Count; i++)
                {
                    if (l.Transitions[i] > MediaTime.Zero)
                    {
                        yield return (p.VideoTrack[i].Id, l.Starts[i], l.Starts[i] + l.Transitions[i]);
                    }
                }

                break;
            }

            case TimelineTrack.AudioMusic:
                foreach (AudioClip a in p.AudioMusicTrack)
                {
                    yield return (a.Id, a.Start, a.End);
                }

                break;
            case TimelineTrack.TitleOverlay:
                foreach (TitleClip t in p.TitleOverlayTrack)
                {
                    yield return (t.Id, t.Start, t.End);
                }

                break;
        }
    }

    private (Row Row, Guid Id, MediaTime Start, MediaTime End, double Edge)? HitClip(Point pt)
    {
        foreach (Row row in Rows())
        {
            if (pt.Y < row.Top || pt.Y >= row.Top + row.Height)
            {
                continue;
            }

            foreach ((Guid id, MediaTime s, MediaTime e) in Spans(row.Track).Reverse())
            {
                double x0 = X(s), x1 = X(e);
                if (pt.X >= x0 - EdgeGrab && pt.X <= x1 + EdgeGrab)
                {
                    double edge = Math.Abs(pt.X - x0) <= EdgeGrab ? -1 : Math.Abs(pt.X - x1) <= EdgeGrab ? 1 : 0;
                    return (row, id, s, e, edge);
                }
            }
        }

        return null;
    }

    public double RowCenter(TimelineTrack track) => Rows().Where(r => r.Track == track).Select(r => r.Top + r.Height / 2).FirstOrDefault(-1);

    private Row? RowAt(double y) => Rows().Cast<Row?>().FirstOrDefault(r => y >= r!.Value.Top && y < r.Value.Top + r.Value.Height);

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        BoardTarget target = BoardTarget.Empty;
        if (!e.TryGetPosition(this, out Point p))
        {
            target = _vm.Session.SelectedClips.Count == 0 ? BoardTarget.Empty : TargetOn(_vm.Session.SelectionTrack, _vm.Session.SelectedClips[0], null);
        }
        else if (p.X >= HeaderWidth && p.Y >= RulerHeight && HitClip(p) is { } hit)
        {
            target = TargetOn(hit.Row.Track, hit.Id, StarRect(hit.Row, hit.Start, hit.End).Contains(p) ? p : null);
        }

        e.Handled = BoardMenus.Show(this, target);
    }

    private BoardTarget TargetOn(TimelineTrack track, Guid id, Point? onStar)
    {
        Project p = _vm!.Project;
        return track switch
        {
            TimelineTrack.Transition => BoardTarget.Transition,
            TimelineTrack.Audio => BoardTarget.Audio,
            TimelineTrack.AudioMusic => BoardTarget.AudioMusic,
            TimelineTrack.TitleOverlay => BoardTarget.TitleOverlay,
            _ => p.VideoTrack.FirstOrDefault(c => c.Id == id) switch
            {
                null => BoardTarget.Empty,
                { Effects.Count: > 0 } when onStar is not null => BoardTarget.EffectStar,
                { Kind: VideoClipKind.Title } => BoardTarget.Title,
                _ => BoardTarget.VideoClip,
            },
        };
    }

    private Rect StarRect(Row row, MediaTime start, MediaTime end)
    {
        if (row.Track != TimelineTrack.Video)
        {
            return default;
        }

        return StarBox(new Rect(X(start) + 0.5, row.Top + 2.5, Math.Max(1, X(end) - X(start) - 1), row.Height - 5));
    }

    private static Rect StarBox(Rect r)
    {
        const double size = 20;
        double tw = (r.Height - 6) * 4 / 3;
        double x = r.X + tw + 14 + size <= r.Right - 2 ? r.X + tw + 14
            : r.Width >= size + 8 ? r.X + 4
            : r.X + 1;
        return new Rect(x, r.Bottom - 24, size, size);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_vm is null)
        {
            return;
        }

        Point p = e.GetPosition(this);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (p.X >= HeaderWidth && p.Y >= RulerHeight && HitClip(p) is { } right)
            {
                if (!_vm.IsSelected(right.Id, right.Row.Track))
                {
                    _vm.Click(right.Row.Track, right.Id, ctrl: false, shift: false);
                }

                if (right.Row.Track == TimelineTrack.Video)
                {
                    _gesture = Gesture.PendingDrag;
                    _pressed = e;
                    _pressPoint = p;
                }
            }

            return;
        }

        if (ExpandBox.Contains(p))
        {
            _vm.ToggleVideoExpandedCommand.Execute(null);
            return;
        }

        if (p.X < HeaderWidth)
        {
            return;
        }

        if (p.Y < RulerHeight)
        {
            _gesture = Gesture.Seek;
            e.Pointer.Capture(this);
            _vm.RequestSeek(T(p.X));
            return;
        }

        bool ctrl = Menus.CommandKeys.HasCommand(e.KeyModifiers);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var hit = HitClip(p);
        if (hit is null)
        {
            if (RowAt(p.Y) is { } emptyRow)
            {
                _vm.ActiveTrack = emptyRow.Track;
                if (!ctrl && !shift)
                {
                    _vm.ClearSelection();
                }

                _gesture = Gesture.Marquee;
                _marquee = (emptyRow.Track, p.X, p.X);
                e.Pointer.Capture(this);
            }

            return;
        }

        (Row row, Guid id, _, _, double edge) = hit.Value;
        bool inMulti = _vm.IsSelected(id, row.Track) && _vm.Session.SelectedClips.Count > 1;
        _vm.Click(row.Track, id, ctrl, shift);
        _collapse = !ctrl && !shift && inMulti ? (row.Track, id) : null;
        TimelineTrack selectTrack = row.Track;

        if (edge != 0 && row.Track is not TimelineTrack.Audio)
        {
            _gesture = Gesture.Trim;
            _gestureClip = id;
            _gestureTrack = row.Track;
            _vm.BeginTrim(id, row.Track, edge < 0 ? TrimEdge.Start : TrimEdge.End);
            e.Pointer.Capture(this);
            return;
        }

        if (selectTrack == TimelineTrack.Video && row.Track == TimelineTrack.Video)
        {
            _gesture = Gesture.PendingDrag;
            _pressed = e;
            _pressPoint = p;
            return;
        }

        if (row.Track is TimelineTrack.AudioMusic or TimelineTrack.TitleOverlay)
        {
            _gesture = Gesture.Move;
            _gestureClip = id;
            _gestureTrack = row.Track;
            _vm.BeginMove(id, row.Track, T(p.X));
            e.Pointer.Capture(this);
        }
    }

    protected override async void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_vm is null)
        {
            return;
        }

        Point p = e.GetPosition(this);
        _vm.SnapDisabled = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        switch (_gesture)
        {
            case Gesture.Seek:
                _vm.RequestSeek(T(Math.Max(HeaderWidth, p.X)));
                return;
            case Gesture.Trim:
                _vm.UpdateTrim(T(p.X));
                return;
            case Gesture.Move:
                if (OverlayTitleOverVideo(p))
                {
                    _drop = (TimelineTrack.Video, Math.Max(HeaderWidth, p.X));
                }
                else
                {
                    _drop = null;
                    _vm.UpdateMove(T(p.X));
                }

                InvalidateVisual();
                return;
            case Gesture.Marquee when _marquee is { } m:
                _marquee = m with { To = Math.Max(HeaderWidth, p.X) };
                InvalidateVisual();
                return;
            case Gesture.PendingDrag when _pressed is { } pressed && Math.Abs(p.X - _pressPoint.X) > 4:
                _gesture = Gesture.None;
                _collapse = null;
                _pressed = null;
                await DragPayload.DoDragAsync(pressed, DragPayload.ForStoryboardMove());
                return;
        }

        var hit = p.X > HeaderWidth && p.Y > RulerHeight ? HitClip(p) : null;
        Cursor = hit is { Edge: not 0 } h && h.Row.Track is not TimelineTrack.Audio ? new Cursor(StandardCursorType.SizeWestEast) : Cursor.Default;
        UpdateTip(p, hit);
    }

    private bool OverlayTitleOverVideo(Point p) =>
        _gestureTrack == TimelineTrack.TitleOverlay && RowAt(p.Y) is { Track: TimelineTrack.Video };

    private string? _tip;

    private void UpdateTip(Point p, (Row Row, Guid Id, MediaTime Start, MediaTime End, double Edge)? hit)
    {
        string? tip = null;
        if (hit is { } h)
        {
            string name = _vm!.Project.VideoTrack.FirstOrDefault(c => c.Id == h.Id) is { } v
                ? (v.Kind == VideoClipKind.Title ? Strings.UntitledTitle : _vm.Project.FindMedia(v.MediaId)?.Clip(v.SourceClipId)?.Name ?? _vm.Project.FindMedia(v.MediaId)?.Name ?? string.Empty)
                : _vm.Project.AudioMusicTrack.FirstOrDefault(a => a.Id == h.Id) is { } a ? _vm.Project.FindMedia(a.MediaId)?.Name ?? string.Empty
                : Strings.UntitledTitle;
            tip = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ContentToolTip, name, TimeFormat.Format(h.End - h.Start));
        }
        else if (p.X > HeaderWidth && RowAt(p.Y) is { } r)
        {
            tip = r.Track switch
            {
                TimelineTrack.AudioMusic => Strings.HintDropAudio,
                TimelineTrack.TitleOverlay => Strings.HintAddTitle,
                TimelineTrack.Video => Strings.HintDropVideo,
                TimelineTrack.Transition => Strings.HintDropTransition,
                _ => null,
            };
        }

        if (tip != _tip)
        {
            _tip = tip;
            ToolTip.SetTip(this, tip);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_vm is null)
        {
            return;
        }

        switch (_gesture)
        {
            case Gesture.Trim:
                _vm.EndTrim();
                break;
            case Gesture.Move:
                Point at = e.GetPosition(this);
                _vm.EndMove(T(Math.Max(HeaderWidth, at.X)), OverlayTitleOverVideo(at) ? TimelineTrack.Video : null);
                _drop = null;
                break;
            case Gesture.Marquee when _marquee is { } m:
                if (Math.Abs(m.To - m.From) > 3)
                {
                    _vm.SelectRange(m.Track, T(m.From), T(m.To), add: Menus.CommandKeys.HasCommand(e.KeyModifiers));
                }

                break;
        }

        if (_collapse is { } c && _gesture is Gesture.PendingDrag or Gesture.None or Gesture.Move)
        {
            _vm.Collapse(c.Track, c.Id);
        }

        _collapse = null;
        _marquee = null;
        _gesture = Gesture.None;
        _pressed = null;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_vm is not null)
        {
            _vm.Session.ActiveSelection = ViewModels.Session.SelectionKind.Timeline;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm is not null && _gesture == Gesture.None)
        {
            KeyModifiers mods = Menus.CommandKeys.ToLogical(e.KeyModifiers);
            bool shift = mods.HasFlag(KeyModifiers.Shift);
            bool ctrl = mods.HasFlag(KeyModifiers.Control);
            bool plain = (mods & (KeyModifiers.Alt | KeyModifiers.Meta)) == 0;
            bool handled = plain && e.Key switch
            {
                Key.Up when !ctrl && !shift => Do(() => _vm.MoveTrack(-1)),
                Key.Down when !ctrl && !shift => Do(() => _vm.MoveTrack(1)),
                Key.Left => _vm.Key(SelectionKey.Previous, shift, ctrl),
                Key.Right => _vm.Key(SelectionKey.Next, shift, ctrl),
                Key.Home => _vm.Key(SelectionKey.First, shift, ctrl),
                Key.End => _vm.Key(SelectionKey.Last, shift, ctrl),
                Key.Space when ctrl || shift => _vm.Key(ctrl ? SelectionKey.ToggleActive : SelectionKey.ExtendToActive),
                Key.Escape => _vm.Key(SelectionKey.Clear),
                _ => false,
            };
            if (handled)
            {
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape && _gesture is Gesture.Trim or Gesture.Move)
        {
            _vm?.CancelGesture();
            _gesture = Gesture.None;
            _drop = null;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    public bool ScrollBy(int direction)
    {
        if (_vm is null)
        {
            return false;
        }

        double visible = Math.Max(1, Bounds.Width - HeaderWidth) / _vm.PixelsPerSecond;
        _vm.ScrollSeconds = Math.Max(0, _vm.ScrollSeconds + direction * visible / 10);
        return true;
    }

    private static bool Do(Action a)
    {
        a();
        return true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_vm is null)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            (e.Delta.Y > 0 ? _vm.ZoomInCommand : _vm.ZoomOutCommand).Execute(null);
        }
        else
        {
            _vm.ScrollSeconds = Math.Max(0, _vm.ScrollSeconds - e.Delta.Y * 40 / _vm.PixelsPerSecond);
        }

        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        DragPayload? payload = DragPayload.From(e);
        _drop = payload is null ? null : DropAt(payload, e.GetPosition(this));
        _dropPayload = payload;
        bool copy = payload is not null && (!payload.IsStoryboardMove || e.KeyModifiers.HasFlag(KeyModifiers.Control));
        e.DragEffects = _drop is null ? DragDropEffects.None : copy ? DragDropEffects.Copy : DragDropEffects.Move;
        AutoScroll(e.GetPosition(this));
        e.Handled = true;
        InvalidateVisual();
    }

    private (TimelineTrack Track, double X)? DropAt(DragPayload payload, Point p)
    {
        if (_vm is null || RowAt(p.Y) is not { } r || p.X <= HeaderWidth)
        {
            return null;
        }

        int at = _vm.Layout.IndexAt(T(p.X));
        bool ok = payload.TransitionId is not null ? r.Track is TimelineTrack.Video or TimelineTrack.Transition && at > 0 && _vm.Session.Editor.TransitionFits(_vm.Project.VideoTrack[at].Id)
            : payload.EffectId is not null ? r.Track == TimelineTrack.Video && at >= 0
            : payload.IsStoryboardMove ? r.Track == TimelineTrack.Video || (r.Track == TimelineTrack.TitleOverlay && _vm.SelectedVideoTitle is not null)
            : TimelineViewModel.Accepts(r.Track, payload.Media);
        return ok ? (r.Track, p.X) : null;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DragPayload? payload = DragPayload.From(e);
        if (_vm is not null && payload is not null && DropAt(payload, e.GetPosition(this)) is { } d)
        {
            e.DragEffects = payload.IsStoryboardMove ? DragDropEffects.Move : DragDropEffects.Copy;
            e.Handled = true;
            bool copy = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (payload.IsRightDrag)
            {
                DragDropEffects choice = await DropMenu.ChooseAsync(this, canMove: payload.IsStoryboardMove);
                if (choice == DragDropEffects.None)
                {
                    EndDrop();
                    return;
                }

                copy = choice == DragDropEffects.Copy;
            }

            MediaTime t = T(d.X);
            if (payload.TransitionId is { } tr)
            {
                _vm.DropTransition(t, tr);
            }
            else if (payload.EffectId is { } fx)
            {
                _vm.DropEffect(t, fx);
            }
            else if (payload.IsStoryboardMove && d.Track == TimelineTrack.TitleOverlay)
            {
                if (_vm.SelectedVideoTitle is { } title)
                {
                    _vm.DropTitleOnOverlay(title, t, copy);
                }
            }
            else if (payload.IsStoryboardMove)
            {
                int index = _vm.Layout.InsertIndexAt(t);
                _ = copy
                    ? _vm.Session.Editor.CopyVideoClips(_vm.Session.SelectedClips.ToList(), index)
                    : _vm.Session.Editor.MoveVideoClips(_vm.Session.SelectedClips.ToList(), index);
            }
            else
            {
                _vm.DropMedia(d.Track, t, payload.Media);
            }
        }

        EndDrop();
    }

    private void EndDrop()
    {
        _drop = null;
        _dropPayload = null;
        _scrollTimer?.Stop();
        InvalidateVisual();
    }

    private Avalonia.Threading.DispatcherTimer? _scrollTimer;
    private int _scrollDirection;

    private void AutoScroll(Point p)
    {
        _scrollDirection = p.X > HeaderWidth && p.X < HeaderWidth + 11 ? -1 : p.X > Bounds.Width - 11 ? 1 : 0;
        if (_scrollDirection == 0 || _vm is null)
        {
            _scrollTimer?.Stop();
            return;
        }

        _scrollTimer ??= new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(50), Avalonia.Threading.DispatcherPriority.Normal, (_, _) =>
        {
            if (_vm is not null && _scrollDirection != 0)
            {
                _vm.ScrollSeconds = Math.Max(0, _vm.ScrollSeconds + _scrollDirection * 16 / _vm.PixelsPerSecond);
            }
        });
        _scrollTimer.Start();
    }

    private IBrush Res(string key, IBrush fallback) => this.Brush(key, fallback);

    private FormattedText Text(string s, double size, IBrush brush, bool bold = false) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(TextElement.GetFontFamily(this), weight: bold ? FontWeight.Bold : FontWeight.Normal), size, brush);

    public override void Render(DrawingContext ctx)
    {
        if (_vm is null)
        {
            return;
        }

        double w = Bounds.Width, h = Bounds.Height;
        List<Row> rows = Rows();
        ctx.FillRectangle(Res("MmLowerBrush", Brushes.LightSteelBlue), new Rect(0, 0, HeaderWidth, h));
        ctx.FillRectangle(Res("MmTrackBrush", Brushes.White), new Rect(HeaderWidth, 0, w - HeaderWidth, h));
        DrawRuler(ctx, w);
        var line = new Pen(Res("MmTrackLineBrush", Brushes.Gray));
        foreach (Row row in rows)
        {
            ctx.DrawLine(line, new Point(HeaderWidth, row.Top + row.Height - 0.5), new Point(w, row.Top + row.Height - 0.5));
            DrawHeader(ctx, row);
            using (ctx.PushClip(new Rect(HeaderWidth, row.Top, w - HeaderWidth, row.Height)))
            {
                DrawRow(ctx, row, w);
            }
        }

        if (_vm.Project.VideoTrack.Count == 0)
        {
            Row video = rows[0];
            ctx.DrawText(Text(Strings.HintDragMedia, 12, Res("MmHintTextBrush", Brushes.Black)), new Point(HeaderWidth + 6, video.Top + 6));
        }

        if (_drop is { } d)
        {
            DrawDropTarget(ctx, rows, d.Track, d.X);
        }

        if (_marquee is { } mq && rows.FirstOrDefault(x => x.Track == mq.Track) is { Height: > 0 } mr)
        {
            var dash = new Pen(Brushes.Black, 1, new DashStyle([2, 2], 0));
            double x0 = Math.Min(mq.From, mq.To), x1 = Math.Max(mq.From, mq.To);
            ctx.DrawRectangle(null, dash, new Rect(x0 + 0.5, mr.Top + 0.5, Math.Max(1, x1 - x0), mr.Height - 1));
        }

        double px = X(_vm.Playhead);
        if (px >= HeaderWidth)
        {
            var pen = new Pen(Res("MmPlayheadBrush", Brushes.Green), 1);
            ctx.DrawLine(pen, new Point(px + 0.5, 0), new Point(px + 0.5, h));
            ctx.DrawRectangle(Res("MmPlayheadBrush", Brushes.Green), null, new Rect(px - 3, 0, 7, 8));
        }
    }

    private void DrawDropTarget(DrawingContext ctx, List<Row> rows, TimelineTrack track, double x)
    {
        Row r = rows.First(row => row.Track == track);
        TimelineLayout layout = _vm!.Layout;
        IBrush accent = Res("MmSelectionBrush", new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF)));
        var pen = new Pen(accent, 2);
        if (_dropPayload?.EffectId is not null && layout.IndexAt(T(x)) is var i and >= 0)
        {
            double x0 = X(layout.Starts[i]), x1 = X(layout.End(i));
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x40, 0x33, 0x99, 0xFF)), pen, new Rect(x0 + 1, r.Top + 1, Math.Max(2, x1 - x0 - 2), r.Height - 2));
        }
        else if (_dropPayload?.TransitionId is not null && layout.IndexAt(T(x)) is var j and > 0)
        {
            double xs = X(layout.Starts[j]);
            double xe = Math.Max(xs + 6, X(layout.Starts[j] + layout.Transitions[j]));
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x40, 0x33, 0x99, 0xFF)), pen, new Rect(xs - 3, r.Top + 1, xe - xs + 6, r.Height - 2));
        }
        else
        {
            double bx = track == TimelineTrack.Video && layout.Count > 0
                ? layout.InsertIndexAt(T(x)) is var k && k < layout.Count ? X(layout.Starts[k]) : X(layout.VideoEnd)
                : x;
            ctx.FillRectangle(Brushes.Black, new Rect(bx - 1, r.Top, 3, r.Height));
        }
    }

    private void DrawRuler(DrawingContext ctx, double w)
    {
        ctx.FillRectangle(Res("MmTrackBrush", Brushes.White), new Rect(HeaderWidth, 0, w - HeaderWidth, RulerHeight));
        double pps = _vm!.PixelsPerSecond;
        double[] steps = [0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1200, 1800, 3600];
        double step = steps.FirstOrDefault(s => s * pps >= 80, 3600);
        IBrush ink = Res("MmRulerTextBrush", new SolidColorBrush(Color.FromRgb(0x00, 0x02, 0x5C)));
        var tick = new Pen(ink);
        var rule = new Pen(Res("MmRulerLineBrush", new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA8))));
        using (ctx.PushClip(new Rect(HeaderWidth, 0, Math.Max(0, w - HeaderWidth), RulerHeight)))
        {
            ctx.DrawLine(rule, new Point(HeaderWidth, 0.5), new Point(w, 0.5));
            ctx.DrawLine(rule, new Point(HeaderWidth, RulerHeight - 0.5), new Point(w, RulerHeight - 0.5));
            int minor = Math.Max(1, (int)Math.Round(step * pps / 6));
            double start = Math.Floor(_vm.ScrollSeconds / step) * step;
            for (double s = start; ; s += step)
            {
                double x = X(MediaTime.FromSeconds(s));
                if (x - step * pps > w)
                {
                    break;
                }

                for (int k = 1; k < minor; k++)
                {
                    double xs = Math.Round(x + k * step * pps / minor) + 0.5;
                    ctx.DrawLine(tick, new Point(xs, 1), new Point(xs, 2));
                }

                ctx.DrawLine(tick, new Point(Math.Round(x) + 0.5, 1), new Point(Math.Round(x) + 0.5, 4));
                FormattedText label = Text(TimeFormat.Format(MediaTime.FromSeconds(s)), 11, ink);
                ctx.DrawText(label, new Point(Math.Round(x - label.Width / 2), Math.Max(3, (RulerHeight - label.Height) / 2 + 1)));
            }
        }
    }

    private void DrawHeader(DrawingContext ctx, Row row)
    {
        IBrush ink = Res("MmTrackHeaderTextBrush", Brushes.Black);
        bool bold = row.Track == _vm!.ActiveTrack;
        FormattedText t = Text(row.Label, 12, ink, bold);
        double right = row.Track == TimelineTrack.Video ? 86 : 84;
        if (row.Track is TimelineTrack.Transition or TimelineTrack.Audio)
        {
            var dots = new Pen(new SolidColorBrush(Color.FromRgb(0x90, 0x98, 0xA4)), 1, new DashStyle([1, 1], 0));
            ctx.DrawLine(dots, new Point(99.5, row.Top), new Point(99.5, row.Top + row.Height / 2));
            ctx.DrawLine(dots, new Point(99.5, row.Top + row.Height / 2), new Point(HeaderWidth, row.Top + row.Height / 2));
        }

        ctx.DrawText(t, new Point(right - t.Width, row.Top + (row.Height - t.Height) / 2));
        if (row.Track == TimelineTrack.Video)
        {
            using (ctx.PushTransform(Matrix.CreateTranslation(ExpandBox.X, row.Top + (row.Height - 16) / 2)))
            {
                Glyph.Draw(ctx, _vm!.IsVideoExpanded ? "collapse" : "expand", this);
            }
        }
    }

    private void DrawRow(DrawingContext ctx, Row row, double w)
    {
        Project p = _vm!.Project;
        IPen border = new Pen(Res("MmClipBorderBrush", Brushes.Black));
        foreach ((Guid id, MediaTime s, MediaTime e) in Spans(row.Track))
        {
            double x0 = X(s), x1 = X(e);
            if (x1 < HeaderWidth || x0 > w)
            {
                continue;
            }

            var r = new Rect(x0 + 0.5, row.Top + 2.5, Math.Max(1, x1 - x0 - 1), row.Height - 5);
            bool selected = _vm.IsSelected(id, row.Track);
            switch (row.Track)
            {
                case TimelineTrack.Video:
                    DrawVideoClip(ctx, r, p.VideoTrack.First(c => c.Id == id), selected, border);
                    break;
                case TimelineTrack.Transition:
                {
                    VideoClip c = p.VideoTrack.First(v => v.Id == id);
                    ctx.DrawRectangle(Res("MmClipBrush", Brushes.LightBlue), border, r);
                    string name = TransitionCatalog.Find(c.TransitionIn!.TransitionId)?.Name ?? string.Empty;
                    using (ctx.PushClip(r))
                    {
                        ctx.DrawText(Text(name, 11, Brushes.Black), new Point(r.X + 3, r.Y + 2));
                    }

                    break;
                }

                case TimelineTrack.Audio:
                {
                    VideoClip c = p.VideoTrack.First(v => v.Id == id);
                    MediaItem? m = p.FindMedia(c.MediaId);
                    ctx.DrawRectangle(selected ? Res("MmClipSelectedBrush", Brushes.White) : Res("MmTrackBrush", Brushes.White), border, r);
                    DrawWave(ctx, r, m, c.In, c.Speed, c.Audio);
                    using (ctx.PushClip(r))
                    {
                        ctx.DrawText(Text(m?.Clip(c.SourceClipId)?.Name ?? m?.Name ?? string.Empty, 11, Brushes.Black), new Point(r.X + 8, r.Y + 2));
                    }

                    break;
                }

                case TimelineTrack.AudioMusic:
                {
                    AudioClip a = p.AudioMusicTrack.First(x => x.Id == id);
                    MediaItem? m = p.FindMedia(a.MediaId);
                    ctx.DrawRectangle(selected ? Res("MmClipSelectedBrush", Brushes.White) : Res("MmTrackBrush", Brushes.White), border, r);
                    DrawWave(ctx, r, m, a.In, 1, a.Audio);
                    using (ctx.PushClip(r))
                    {
                        ctx.DrawText(Text(m?.Clip(a.SourceClipId)?.Name ?? m?.Name ?? string.Empty, 11, Brushes.Black), new Point(r.X + 8, r.Y + 2));
                    }

                    break;
                }

                case TimelineTrack.TitleOverlay:
                {
                    TitleClip t = p.TitleOverlayTrack.First(x => x.Id == id);
                    ctx.DrawRectangle(selected ? Res("MmClipSelectedBrush", Brushes.White) : Res("MmTitleClipBrush", Brushes.Lavender), border, r);
                    using (ctx.PushClip(r))
                    {
                        ctx.DrawText(Text(t.Content.Summary, 11, Brushes.Black), new Point(r.X + 4, r.Y + 2));
                    }

                    break;
                }
            }
        }

        if (row.Track == TimelineTrack.Video)
        {
            TimelineLayout l = TimelineLayout.Compute(p);
            var shade = new SolidColorBrush(Color.FromArgb(0x50, 0x30, 0x40, 0x58));
            var edge = new Pen(Res("MmClipBorderBrush", Brushes.Black));
            for (int i = 1; i < l.Count; i++)
            {
                if (l.Transitions[i] <= MediaTime.Zero)
                {
                    continue;
                }

                double x0 = X(l.Starts[i]), x1 = X(l.Starts[i] + l.Transitions[i]);
                var band = new Rect(x0 + 0.5, row.Top + 2.5, Math.Max(1, x1 - x0), row.Height - 5);
                ctx.FillRectangle(shade, band);
                ctx.DrawLine(edge, new Point(x1 + 0.5, band.Top), new Point(x1 + 0.5, band.Bottom));
                ctx.DrawGeometry(Brushes.Black, null, new PolylineGeometry(
                    [new Point(x0 + 2, band.Center.Y), new Point(x0 + 6, band.Center.Y - 4), new Point(x0 + 6, band.Center.Y + 4)], true));
            }
        }
    }

    private void DrawVideoClip(DrawingContext ctx, Rect r, VideoClip c, bool selected, IPen border)
    {
        ctx.DrawRectangle(selected ? Res("MmClipSelectedBrush", Brushes.White) : Res("MmClipBrush", Brushes.LightBlue), border, r);
        Project p = _vm!.Project;
        MediaItem? m = c.Kind == VideoClipKind.Title ? null : p.FindMedia(c.MediaId);
        double th = r.Height - 6;
        double tw = th * 4 / 3;
        string name = c.Kind == VideoClipKind.Title ? c.Title?.Summary ?? Strings.UntitledTitle : m?.Clip(c.SourceClipId)?.Name ?? m?.Name ?? string.Empty;
        using (ctx.PushClip(r.Deflate(1)))
        {
            var thumbRect = new Rect(r.X + 4, r.Y + 3, tw, th);
            if (r.Width > 6)
            {
                ctx.FillRectangle(Brushes.Black, thumbRect);
                if (c.Kind == VideoClipKind.Title && c.Title is not null)
                {
                    ctx.FillRectangle(new SolidColorBrush(Color.FromUInt32(c.Title.BackgroundColor)), thumbRect);
                }
                else if (m is not null)
                {
                    MediaTime at = c.IsStill ? MediaTime.Zero : Thumbnailer.RepresentativeTime(c.In, c.Out - c.In);
                    double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
                    Task<SkiaSharp.SKBitmap?> t = _vm.FilmstripFrameAsync(c, at, (int)Math.Ceiling(tw * scale), (int)Math.Ceiling(th * scale));
                    if (t.IsCompletedSuccessfully && t.Result is { } sk)
                    {
                        Avalonia.Media.Imaging.Bitmap bmp = SkBitmapConverter.ToBitmap(sk);
                        ctx.DrawImage(bmp, new Rect(bmp.Size), thumbRect);
                    }
                    else if (!t.IsCompleted)
                    {
                        t.ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual), TaskScheduler.Default);
                    }
                }
            }

            if (r.Width - tw - 18 >= 24)
            {
                ctx.DrawText(Text(name, 12, Brushes.Black), new Point(r.X + tw + 14, r.Y + 6));
            }
            if (c.Effects.Count > 0)
            {
                var star = new Glyph { Kind = c.Effects.Count > 1 ? "star-multi" : "star-on" };
                Rect box = StarBox(r);
                using (ctx.PushTransform(Matrix.CreateScale(0.8, 0.8) * Matrix.CreateTranslation(box.X, box.Y)))
                {
                    star.Render(ctx);
                }
            }
        }
    }

    private void DrawWave(DrawingContext ctx, Rect r, MediaItem? m, MediaTime sourceIn, double speed, AudioSettings audio)
    {
        if (audio.Mute || m is null || _vm!.WaveformFor(m.Id) is not { } wave)
        {
            return;
        }

        double gain = audio.Volume;

        double secondsPerPixel = speed / _vm.PixelsPerSecond;
        (float[] min, float[] max, double bucket) = wave.LevelFor(secondsPerPixel);
        double top = r.Y + 15, bottom = r.Bottom - 2;
        if (bottom - top < 6)
        {
            top = r.Y + 2;
        }

        if (bottom - top < 3)
        {
            return;
        }

        double mid = (top + bottom) / 2;
        double amp = (bottom - top) / 2;
        var pen = new Pen(Res("MmWaveformBrush", Brushes.SteelBlue));
        for (double x = Math.Floor(Math.Max(r.X, HeaderWidth)); x < r.Right; x++)
        {
            double src = sourceIn.Seconds + (x - r.X) * secondsPerPixel;
            int i = (int)(src / bucket);
            if (i < 0 || i >= min.Length)
            {
                continue;
            }

            double hi = Math.Clamp(max[i] * gain, -1, 1), lo = Math.Clamp(min[i] * gain, -1, 1);
            ctx.DrawLine(pen, new Point(x + 0.5, mid - hi * amp), new Point(x + 0.5, mid - lo * amp));
        }
    }
}
