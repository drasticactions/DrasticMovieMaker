namespace AvaMovieMaker.Media.Decoding;

public readonly record struct FramePlane(IntPtr Data, int Stride, int Width, int Height);
