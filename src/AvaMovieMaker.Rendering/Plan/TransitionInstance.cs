namespace AvaMovieMaker.Rendering.Plan;

public sealed record TransitionInstance(string TransitionId, double Progress, int Seed, double DurationSeconds);
