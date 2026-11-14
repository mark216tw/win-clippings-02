namespace Clippings.Platform.Interop;

public sealed record WindowRecordingTarget(
    WindowCaptureTarget Target,
    string Title,
    string ProcessName)
{
    public string? ExecutablePath { get; init; }

    public string DisplayName => $"{Title}  -  {ProcessName}";
}
