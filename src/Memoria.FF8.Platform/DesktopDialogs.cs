namespace Memoria.FF8.Platform;

public enum DialogStatus
{
    Selected,
    Cancelled,
    Failed
}

public sealed record DialogResult(DialogStatus Status, string? Path = null, string? Error = null)
{
    public IReadOnlyList<string>? Paths { get; init; }
}
public sealed record FileDialogFilter(string Name, IReadOnlyList<string> Extensions);
public sealed record FileDialogRequest(string Title, string? InitialPath = null, IReadOnlyList<FileDialogFilter>? Filters = null)
{
    public bool AllowMultiple { get; init; }
}

/// <summary>Desktop adapters own thread dispatch, native callback lifetime and focus restoration.</summary>
public interface IDesktopDialogs
{
    Task<DialogResult> OpenFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default);
    Task<DialogResult> SaveFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default);
    Task<DialogResult> SelectFolderAsync(FileDialogRequest request, CancellationToken cancellationToken = default);
}
