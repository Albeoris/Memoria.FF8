using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class GraphicsRestartSnapshot
{
    internal static UpdateSaveResult SaveTitle(GameSession session, string path, DateTimeOffset expiresAt)
    {
        GameState state = session.PrepareSnapshot();
        if (state.Scene != SceneKind.Title)
        {
            throw new InvalidOperationException("Gameplay graphics restart requires the validated gameplay save handoff.");
        }
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("A graphics restart requires an absolute standalone save path.", nameof(path));
        }
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new TimeoutException("The graphics restart request expired before saving.");
        }
        string staging = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try
        {
            new GameSession(state).Save(staging, compact: true, candidate =>
            {
                if (candidate.Scene != SceneKind.Title)
                {
                    throw new InvalidDataException("The title restart snapshot changed scenes.");
                }
            });
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                throw new TimeoutException("The graphics restart request expired before publishing its save.");
            }
            File.Move(staging, path);
            return new UpdateSaveResult(true, path);
        }
        finally
        {
            File.Delete(staging);
        }
    }
}
