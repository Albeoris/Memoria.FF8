namespace Memoria.FF8;

internal static class CrossoverControlPolicy
{
    internal static void EnsureAllowed(string command, bool ff7Active, bool updatePaused)
    {
        string normalized = command.ToLowerInvariant();
        if (updatePaused && normalized is not ("status" or "world-status" or "cancel-update" or "graphics-restart" or "quit"))
        {
            throw new InvalidOperationException("The game is paused for an update. Cancel the update before changing the game.");
        }
        if (ff7Active && !normalized.StartsWith("world-", StringComparison.Ordinal) && normalized is not ("status" or "capture" or "save" or "save-for-update" or "cancel-update" or "graphics-restart" or "quit"))
        {
            throw new InvalidOperationException("This command belongs to FFVIII. Use the active FFVII world's controls.");
        }
    }
}
