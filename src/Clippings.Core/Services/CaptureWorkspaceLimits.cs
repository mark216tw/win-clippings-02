namespace Clippings.Core.Services;

public static class CaptureWorkspaceLimits
{
    public const int MaxTabs = 20;

    public static bool CanAddTab(int currentCount) => currentCount < MaxTabs;
}
