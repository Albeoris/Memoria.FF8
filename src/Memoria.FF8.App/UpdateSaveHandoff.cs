using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record UpdateSaveResult(bool Saved, string Path);

/// <summary>Publishes a validated standalone continuation without replacing any existing save.</summary>
internal sealed class UpdateSaveHandoff(Func<GameState> capture, Action<GameState> validateContinuation, Action ensureSafe, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    public bool Paused { get; private set; }

    public UpdateSaveResult Save(string? path, DateTimeOffset? expiresAt)
    {
        if (Paused)
        {
            throw new InvalidOperationException("An update save is already prepared. Cancel the update before preparing another save.");
        }
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("An update save requires a unique absolute path.", nameof(path));
        }
        if (expiresAt is null)
        {
            throw new ArgumentException("An update save requires an expiration deadline.", nameof(expiresAt));
        }
        EnsureNotExpired(expiresAt.Value);
        string fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            throw new IOException("The update save destination already exists.");
        }
        ensureSafe();
        GameState state = capture();
        if (state.Scene == SceneKind.Title)
        {
            throw new InvalidOperationException("Start or load a game before preparing an update save.");
        }
        string staging = fullPath + "." + Guid.NewGuid().ToString("N") + ".pending";
        try
        {
            new GameSession(state).Save(staging, compact: true, validateContinuation);
            EnsureNotExpired(expiresAt.Value);
            File.Move(staging, fullPath);
            Paused = true;
            return new UpdateSaveResult(true, fullPath);
        }
        finally
        {
            if (File.Exists(staging))
            {
                File.Delete(staging);
            }
        }
    }

    public void Cancel()
    {
        Paused = false;
    }

    private void EnsureNotExpired(DateTimeOffset expiresAt)
    {
        if (expiresAt <= clock.GetUtcNow())
        {
            throw new TimeoutException("The update save request expired before its save could be published.");
        }
    }
}
