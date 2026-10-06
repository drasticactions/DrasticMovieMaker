using System.Runtime.CompilerServices;
using AvaMovieMaker.IO;

namespace AvaMovieMaker.Tests;

internal static class TestPaths
{
    [ModuleInitializer]
    internal static void Init()
    {
        AppPaths.RootOverride ??= Path.Combine(Path.GetTempPath(), "AvaMovieMaker-tests", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
