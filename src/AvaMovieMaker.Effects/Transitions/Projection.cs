using SkiaSharp;

namespace AvaMovieMaker.Effects.Transitions;

internal static class Projection
{
    public static SKPoint Project(double x, double y, double z, double width, double height)
    {
        double f = height * 1.5;
        double s = f / (f + z);
        return new SKPoint((float)(width / 2 + x * s), (float)(height / 2 + y * s));
    }

    public static SKMatrix RectToQuad(float w, float h, SKPoint q0, SKPoint q1, SKPoint q2, SKPoint q3)
    {
        double x0 = q0.X, y0 = q0.Y, x1 = q1.X, y1 = q1.Y, x2 = q2.X, y2 = q2.Y, x3 = q3.X, y3 = q3.Y;
        double sx = x0 - x1 + x2 - x3;
        double sy = y0 - y1 + y2 - y3;
        double a, b, c, d, e, f, g, hh;
        if (Math.Abs(sx) < 1e-9 && Math.Abs(sy) < 1e-9)
        {
            a = x1 - x0; b = x2 - x1; c = x0;
            d = y1 - y0; e = y2 - y1; f = y0;
            g = 0; hh = 0;
        }
        else
        {
            double dx1 = x1 - x2, dx2 = x3 - x2, dy1 = y1 - y2, dy2 = y3 - y2;
            double den = dx1 * dy2 - dx2 * dy1;
            g = (sx * dy2 - dx2 * sy) / den;
            hh = (dx1 * sy - sx * dy1) / den;
            a = x1 - x0 + g * x1; b = x3 - x0 + hh * x3; c = x0;
            d = y1 - y0 + g * y1; e = y3 - y0 + hh * y3; f = y0;
        }

        var unit = new SKMatrix((float)a, (float)b, (float)c, (float)d, (float)e, (float)f, (float)g, (float)hh, 1);
        if (Math.Abs(sx) < 1e-9 && Math.Abs(sy) < 1e-9)
        {
            unit = new SKMatrix((float)(x1 - x0), (float)(x3 - x0), (float)x0, (float)(y1 - y0), (float)(y3 - y0), (float)y0, 0, 0, 1);
        }

        return SKMatrix.Concat(unit, SKMatrix.CreateScale(1f / w, 1f / h));
    }

    public static SKMatrix RotateY(double angle, int width, int height)
    {
        double hw = width / 2.0, hh = height / 2.0;
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        SKPoint P(double x, double y) => Project(x * cos, y, x * sin, width, height);
        return RectToQuad(width, height, P(-hw, -hh), P(hw, -hh), P(hw, hh), P(-hw, hh));
    }
}
