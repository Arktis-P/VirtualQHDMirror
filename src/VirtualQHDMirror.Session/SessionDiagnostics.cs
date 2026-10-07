using System;
using System.IO;

namespace VirtualQHDMirror.Session;

internal static class SessionDiagnostics
{
    private static readonly object Gate = new();

    public static string LogPath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "VirtualQHDMirror",
            "logs",
            "native-session.log");

    public static void Reset()
    {
        try
        {
            var directory =
                Path.GetDirectoryName(
                    LogPath);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            File.WriteAllText(
                LogPath,
                string.Empty);
        }
        catch
        {
            // Diagnostics must never prevent session startup.
        }
    }

    public static void Write(
        string message)
    {
        try
        {
            lock (Gate)
            {
                var directory =
                    Path.GetDirectoryName(
                        LogPath);

                if (!string.IsNullOrWhiteSpace(
                        directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }

                File.AppendAllText(
                    LogPath,
                    $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never become a session failure.
        }
    }
}
