using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Interop.Mswmm;

public sealed record MswmmImportResult(Project Project, IReadOnlyList<string> Warnings, IReadOnlyList<string> MissingMedia);
