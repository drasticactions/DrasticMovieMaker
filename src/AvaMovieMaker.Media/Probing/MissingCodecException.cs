namespace AvaMovieMaker.Media.Probing;

public sealed class MissingCodecException(string path, string codec) : Exception($"No decoder for {codec} in {System.IO.Path.GetFileName(path)}.")
{
    public string Codec { get; } = codec;
}
