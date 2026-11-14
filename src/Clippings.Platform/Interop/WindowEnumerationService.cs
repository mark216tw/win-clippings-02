using System.Diagnostics;
using System.Text;

namespace Clippings.Platform.Interop;

public sealed class WindowEnumerationService
{
    public IReadOnlyList<WindowRecordingTarget> GetRecordingTargets()
    {
        var targets = new List<WindowRecordingTarget>();
        var seen = new HashSet<IntPtr>();
        _ = NativeMethods.EnumWindows((handle, _) =>
        {
            if (!seen.Add(handle) || !WindowHelper.TryCreateCaptureTarget(handle, out var target))
            {
                return true;
            }

            var titleLength = NativeMethods.GetWindowTextLength(handle);
            if (titleLength <= 0)
            {
                return true;
            }

            var title = new StringBuilder(titleLength + 1);
            if (NativeMethods.GetWindowText(handle, title, title.Capacity) <= 0 ||
                string.IsNullOrWhiteSpace(title.ToString()))
            {
                return true;
            }

            var processInfo = GetProcessInfo(target.ProcessId);
            targets.Add(new WindowRecordingTarget(
                target,
                title.ToString().Trim(),
                processInfo.Name)
            {
                ExecutablePath = processInfo.ExecutablePath,
            });
            return true;
        }, IntPtr.Zero);

        return targets
            .OrderBy(target => target.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(target => target.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static (string Name, string? ExecutablePath) GetProcessInfo(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return (process.ProcessName, TryGetExecutablePath(process));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return ($"PID {processId}", null);
        }
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }
}
