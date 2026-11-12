using Microsoft.Win32;

namespace Clippings.Platform.Storage;

public static class StartupRegistrar
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Clippings.App.New";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            var executable = Environment.ProcessPath;
            var desired = enabled && !string.IsNullOrWhiteSpace(executable)
                ? $"\"{executable}\" --autostart"
                : null;

            using (var readKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false))
            {
                if (string.Equals(readKey?.GetValue(ValueName) as string, desired, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return false;
            }

            if (desired is null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            key.SetValue(ValueName, desired);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }
}
