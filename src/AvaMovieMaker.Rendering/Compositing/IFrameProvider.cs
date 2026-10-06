using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Rendering.Compositing;

public interface IFrameProvider
{
    DecodedFrame? GetFrame(string path, MediaTime time);
}
