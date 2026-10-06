using System.Globalization;
using AvaMovieMaker.Media;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.ViewModels.Dialogs;

public sealed class ClipPropertiesViewModel(string name, IReadOnlyList<(string Label, string Value)> rows)
{
    public string Name { get; } = name;

    public IReadOnlyList<(string Label, string Value)> Rows { get; } = rows;

    public static ClipPropertiesViewModel For(MediaItem m, SourceClip? clip, MediaTime? length = null)
    {
        var rows = new List<(string, string)>
        {
            (Strings.PropertyType, m.Kind switch { MediaKind.Video => Strings.KindVideo, MediaKind.Audio => Strings.KindAudio, _ => Strings.KindPicture }),
        };
        if (m.Kind != MediaKind.Picture)
        {
            rows.Add((Strings.PropertyDuration, TimeFormat.Format(length ?? clip?.Duration ?? m.Duration)));
        }

        if (clip is not null && m.Clips.Count > 1)
        {
            rows.Add((Strings.PropertyStartTime, TimeFormat.Format(clip.Start)));
            rows.Add((Strings.PropertyEndTime, TimeFormat.Format(clip.End)));
        }

        if (m.Video is { } v)
        {
            rows.Add((Strings.PropertyDimensions, string.Format(CultureInfo.CurrentCulture, Strings.Dimensions, v.Width, v.Height)));
            if (m.Kind == MediaKind.Video)
            {
                rows.Add((Strings.PropertyFrameRate, string.Format(CultureInfo.CurrentCulture, Strings.FrameRate, v.FrameRateNum / (double)v.FrameRateDen)));
            }

            rows.Add((Strings.PropertyVideoCodec, v.Codec));
        }

        if (m.Audio is { } a)
        {
            rows.Add((Strings.PropertyAudio, string.Format(CultureInfo.CurrentCulture, a.Channels == 1 ? Strings.AudioFormatOneChannel : Strings.AudioFormat, a.Codec, a.SampleRate, a.Channels)));
        }

        rows.Add((Strings.PropertySource, m.Path));
        rows.Add((Strings.PropertySize, string.Format(CultureInfo.CurrentCulture, Strings.SizeKilobytes, m.FileSize / 1024.0)));
        if (m.DateTaken is { } d)
        {
            rows.Add((Strings.PropertyDateTaken, d.ToString("g", CultureInfo.CurrentCulture)));
        }

        if (m.Missing)
        {
            rows.Add((Strings.PropertyStatus, Strings.FileNotFound));
        }

        return new ClipPropertiesViewModel(clip?.Name ?? m.Name, rows);
    }
}
