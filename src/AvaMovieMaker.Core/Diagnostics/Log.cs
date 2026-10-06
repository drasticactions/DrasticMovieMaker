using System.Globalization;
using AvaMovieMaker.IO;

namespace AvaMovieMaker.Diagnostics;

public static class Log
{
    private static readonly Lock Gate = new();
    private static StreamWriter? _file;

    public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public static bool EchoToConsole { get; set; }

    public static event Action<LogLevel, string, string>? Written;

    public static void OpenFile(string path)
    {
        lock (Gate)
        {
            _file?.Dispose();
            _file = new StreamWriter(FileStore.Current.Create(path)) { AutoFlush = true };
        }
    }

    public static void Write(LogLevel level, string category, string message)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        string line = string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:HH:mm:ss.fff} {Short(level)} [{category}] {message}");
        lock (Gate)
        {
            _file?.WriteLine(line);
            if (EchoToConsole)
            {
                Console.Error.WriteLine(line);
            }
        }

        Written?.Invoke(level, category, message);
    }

    public static void Debug(string category, string message) => Write(LogLevel.Debug, category, message);

    public static void Info(string category, string message) => Write(LogLevel.Info, category, message);

    public static void Warn(string category, string message) => Write(LogLevel.Warning, category, message);

    public static void Error(string category, string message) => Write(LogLevel.Error, category, message);

    public static void Error(string category, string message, Exception e) => Write(LogLevel.Error, category, $"{message}: {e}");

    private static string Short(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warning => "WRN",
        _ => "ERR",
    };
}
