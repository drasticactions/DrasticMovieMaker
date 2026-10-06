using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Export;

public readonly record struct ExportProgress(double Fraction, TimeSpan Remaining, long Frame, long TotalFrames);
