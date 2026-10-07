namespace AvaMovieMaker.Rendering.Compositing;

// How a source whose shape differs from the frame is drawn into it.
public enum FrameFitMode
{
    // The whole source, centered, with black bars.
    Fit,

    // Scaled to cover the frame and center cropped.
    Fill,

    // Fitted over a blurred, darkened copy that covers the frame.
    Blur,
}
