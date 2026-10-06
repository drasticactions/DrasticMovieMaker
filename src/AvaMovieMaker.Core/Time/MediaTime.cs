using System.Globalization;

namespace AvaMovieMaker.Time;

public readonly record struct MediaTime(long Ticks) : IComparable<MediaTime>
{
    public static readonly MediaTime Zero = new(0);
    public static readonly MediaTime MaxValue = new(long.MaxValue);

    public double Seconds => Ticks / (double)TimeSpan.TicksPerSecond;

    public TimeSpan ToTimeSpan() => new(Ticks);

    public static MediaTime FromSeconds(double seconds) => new((long)Math.Round(seconds * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero));

    public static MediaTime FromMilliseconds(double ms) => FromSeconds(ms / 1000.0);

    public static MediaTime FromTimeSpan(TimeSpan span) => new(span.Ticks);

    public static MediaTime FromTimeBase(long value, Rational timeBase) => new(Rational.RescaleQ(value, timeBase, Rational.Ticks));

    public long ToTimeBase(Rational timeBase) => Rational.RescaleQ(Ticks, Rational.Ticks, timeBase);

    public static MediaTime FromFrame(long index, Rational rate) => new(Rational.Rescale(index, TimeSpan.TicksPerSecond * rate.Den, rate.Num));

    public long ToFrameFloor(Rational rate)
    {
        Int128 n = (Int128)Ticks * rate.Num;
        Int128 d = (Int128)TimeSpan.TicksPerSecond * rate.Den;
        Int128 q = n / d;
        if (n < 0 && q * d != n)
        {
            q -= 1;
        }

        return (long)q;
    }

    public long ToFrameNearest(Rational rate) => Rational.Rescale(Ticks, rate.Num, TimeSpan.TicksPerSecond * rate.Den);

    public MediaTime SnapToFrame(Rational rate) => FromFrame(ToFrameNearest(rate), rate);

    public static MediaTime FrameDuration(Rational rate) => FromFrame(1, rate);

    public static MediaTime Min(MediaTime a, MediaTime b) => a.Ticks <= b.Ticks ? a : b;

    public static MediaTime Max(MediaTime a, MediaTime b) => a.Ticks >= b.Ticks ? a : b;

    public MediaTime Clamp(MediaTime min, MediaTime max) => Max(min, Min(max, this));

    public static MediaTime operator +(MediaTime a, MediaTime b) => new(a.Ticks + b.Ticks);

    public static MediaTime operator -(MediaTime a, MediaTime b) => new(a.Ticks - b.Ticks);

    public static MediaTime operator -(MediaTime a) => new(-a.Ticks);

    public static MediaTime operator *(MediaTime a, double f) => new((long)Math.Round(a.Ticks * f, MidpointRounding.AwayFromZero));

    public static MediaTime operator /(MediaTime a, double f) => new((long)Math.Round(a.Ticks / f, MidpointRounding.AwayFromZero));

    public static double operator /(MediaTime a, MediaTime b) => (double)a.Ticks / b.Ticks;

    public static bool operator <(MediaTime a, MediaTime b) => a.Ticks < b.Ticks;

    public static bool operator >(MediaTime a, MediaTime b) => a.Ticks > b.Ticks;

    public static bool operator <=(MediaTime a, MediaTime b) => a.Ticks <= b.Ticks;

    public static bool operator >=(MediaTime a, MediaTime b) => a.Ticks >= b.Ticks;

    public int CompareTo(MediaTime other) => Ticks.CompareTo(other.Ticks);

    public override string ToString() => TimeFormat.Format(this);

    public string ToSecondsString() => Seconds.ToString("0.#######", CultureInfo.InvariantCulture);
}
