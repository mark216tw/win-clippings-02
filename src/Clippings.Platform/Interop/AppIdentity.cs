namespace Clippings.Platform.Interop;

public static class AppIdentity
{
    public const string ApplicationUserModelId = "Clippings.App.New";

    public static bool TryApply()
        => NativeMethods.SetCurrentProcessExplicitAppUserModelID(ApplicationUserModelId) >= 0;
}
