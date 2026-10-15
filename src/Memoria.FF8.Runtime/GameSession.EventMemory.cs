namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateEventMemoryOwners(GameState state, NativeEventMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        NativeEventMemoryOwnership.Validate(memory, NativeEventMemoryOwnership.RootRegions(state));
    }
}
