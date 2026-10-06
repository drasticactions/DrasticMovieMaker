using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Dialogs;

public sealed partial class ProgressViewModel(string title) : ObservableObject
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly CancellationTokenSource _cancel = new();

    public string Title { get; } = title;

    public CancellationToken Token => _cancel.Token;

    [ObservableProperty]
    public partial string Header { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial double Value { get; private set; }

    [ObservableProperty]
    public partial string Status { get; private set; } = Strings.TooSoonToEstimate;

    [ObservableProperty]
    public partial string Percent { get; private set; } = "0%";

    public event EventHandler? Finished;

    public void Report(double done, int total, string name)
    {
        Header = name;
        double fraction = total <= 0 ? 0 : Math.Clamp(done / total, 0, 1);
        Value = fraction * 100;
        Percent = string.Format(CultureInfo.CurrentCulture, "{0}%", (int)(fraction * 100));
        double elapsed = _watch.Elapsed.TotalSeconds;
        Status = done <= 0 || elapsed < 2 ? Strings.TooSoonToEstimate : Remaining(elapsed / done * (total - done));
    }

    private static string Remaining(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalDays >= 1 ? string.Format(CultureInfo.CurrentCulture, Strings.DaysRemaining, (int)t.TotalDays)
            : t.TotalHours >= 1 ? string.Format(CultureInfo.CurrentCulture, Strings.HoursRemaining, (int)t.TotalHours)
            : t.TotalMinutes >= 1 ? string.Format(CultureInfo.CurrentCulture, Strings.MinutesRemaining, (int)t.TotalMinutes)
            : string.Format(CultureInfo.CurrentCulture, Strings.SecondsRemaining, (int)Math.Ceiling(t.TotalSeconds));
    }

    [RelayCommand]
    private void Cancel() => _cancel.Cancel();

    public void Finish() => Finished?.Invoke(this, EventArgs.Empty);
}
