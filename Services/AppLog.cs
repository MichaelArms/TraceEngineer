using System.Diagnostics;
using System.IO;
using System.Text;

namespace TraceEngineer.Services;

/// <summary>
/// Minimal file logger. Writes to %APPDATA%\TraceEngineer\TraceEngineer.log
/// (rotated to TraceEngineer.log.old above 512 KB) and mirrors to the debugger.
/// Never throws: logging must not be able to break the app.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TraceEngineer", "TraceEngineer.log");

    /// <summary>Expected/benign condition (e.g. an optional channel table is missing).</summary>
    public static void Info(string context, Exception? ex = null) => Write("INFO", context, ex);

    /// <summary>Something went wrong but the app carries on (e.g. a corrupt saved lap is skipped).</summary>
    public static void Warn(string context, Exception? ex = null) => Write("WARN", context, ex);

    private static void Write(string level, string context, Exception? ex)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {context}" +
                   (ex != null ? $" -> {ex.GetType().Name}: {ex.Message}" : "");

        Debug.WriteLine(line);

        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(LogPath, LogPath + ".old", overwrite: true);

                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Intentionally ignored: a failing logger must never affect the app.
        }
    }
}
