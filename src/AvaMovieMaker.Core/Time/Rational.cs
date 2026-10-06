namespace AvaMovieMaker.Time;

public readonly record struct Rational(long Num, long Den)
{
    public static readonly Rational Ntsc = new(30000, 1001);

    public static readonly Rational Pal = new(25, 1);

    public static readonly Rational Ticks = new(1, TimeSpan.TicksPerSecond);

    public bool IsValid => Num > 0 && Den > 0;

    public double ToDouble() => Den == 0 ? 0 : (double)Num / Den;

    public Rational Invert() => new(Den, Num);

    public override string ToString() => $"{Num}/{Den}";

    public static long Rescale(long a, long b, long c)
    {
        if (c == 0)
        {
            throw new DivideByZeroException();
        }

        Int128 product = (Int128)a * b;
        Int128 divisor = c;
        if (divisor < 0)
        {
            product = -product;
            divisor = -divisor;
        }

        Int128 half = divisor / 2;
        Int128 q = product >= 0 ? (product + half) / divisor : -((-product + half) / divisor);
        return (long)q;
    }

    public static long RescaleQ(long value, Rational from, Rational to) =>
        Rescale(value, from.Num * to.Den, from.Den * to.Num);
}
