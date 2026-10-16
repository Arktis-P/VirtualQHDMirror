using Microsoft.Win32;

namespace VirtualQHDMirror.App;

internal sealed class WindowsStartupRegistrationService
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName =
        "VirtualQHDMirror";

    public bool IsEnabled()
    {
        using var key =
            Registry.CurrentUser.OpenSubKey(
                RunKeyPath,
                writable: false);

        return key?.GetValue(
                ValueName) is string value &&
            !string.IsNullOrWhiteSpace(
                value);
    }

    public void Enable()
    {
        var command =
            BuildCurrentExecutableCommand();

        using var key =
            Registry.CurrentUser.CreateSubKey(
                RunKeyPath,
                writable: true) ??
            throw new InvalidOperationException(
                "Could not open the current-user Windows startup registry key.");

        key.SetValue(
            ValueName,
            command,
            RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key =
            Registry.CurrentUser.OpenSubKey(
                RunKeyPath,
                writable: true);

        key?.DeleteValue(
            ValueName,
            throwOnMissingValue: false);
    }

    public void RefreshCurrentExecutableIfEnabled()
    {
        if (!IsEnabled())
        {
            return;
        }

        Enable();
    }

    private static string BuildCurrentExecutableCommand()
    {
        var executablePath =
            Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(
                executablePath))
        {
            throw new InvalidOperationException(
                "Could not determine the VirtualQHDMirror executable path.");
        }

        return $"\"{executablePath}\"";
    }
}
