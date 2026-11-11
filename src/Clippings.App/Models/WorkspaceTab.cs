using CommunityToolkit.Mvvm.ComponentModel;

namespace Clippings.App.Models;

public sealed class WorkspaceTab : ObservableObject
{
    private bool _isSaved;

    public required DateTimeOffset CreatedAt { get; init; }

    public required string Title { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string? TempPath { get; set; }

    public byte[]? MemoryImage { get; set; }

    public bool HasTempFile => !string.IsNullOrWhiteSpace(TempPath);

    public bool IsSaved
    {
        get => _isSaved;
        set => SetProperty(ref _isSaved, value);
    }
}
