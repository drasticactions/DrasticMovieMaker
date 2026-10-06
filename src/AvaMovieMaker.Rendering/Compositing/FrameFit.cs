using SkiaSharp;

namespace AvaMovieMaker.Rendering.Compositing;

public static class FrameFit
{
    public static SKMatrix Matrix(int srcWidth, int srcHeight, double sampleAspect, int rotation, bool flip, int outWidth, int outHeight, double outPixelAspect)
    {
        double dw = srcWidth * sampleAspect;
        double dh = srcHeight;
        if (rotation is 90 or 270)
        {
            (dw, dh) = (dh, dw);
        }

        double outDw = outWidth * outPixelAspect;
        double outDh = outHeight;
        double s = Math.Min(outDw / dw, outDh / dh);

        SKMatrix m = SKMatrix.CreateTranslation(-srcWidth / 2f, -srcHeight / 2f);
        m = m.PostConcat(SKMatrix.CreateScale((float)sampleAspect, 1));
        m = m.PostConcat(SKMatrix.CreateRotationDegrees(rotation));
        if (flip)
        {
            m = m.PostConcat(SKMatrix.CreateScale(-1, 1));
        }

        m = m.PostConcat(SKMatrix.CreateScale((float)s, (float)s));
        m = m.PostConcat(SKMatrix.CreateScale((float)(1 / outPixelAspect), 1));
        m = m.PostConcat(SKMatrix.CreateTranslation(outWidth / 2f, outHeight / 2f));
        return m;
    }

    public static SKRect Rect(SKMatrix m, int srcWidth, int srcHeight)
    {
        SKRect r = m.MapRect(new SKRect(0, 0, srcWidth, srcHeight));
        return r;
    }
}
