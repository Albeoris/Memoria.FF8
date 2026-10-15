namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateEventMemoryOwners(GameState state, NativeEventMemory memory)
    {
        ValidateEventMemoryOwners(state, memory, NativeEventSceneMemoryOwners.None);
    }

    private static void ValidateEventMemoryOwners(GameState state, NativeEventMemory memory, NativeEventSceneMemoryOwners liveOwners)
    {
        ArgumentNullException.ThrowIfNull(memory);
        NativeEventSceneMemoryOwners sceneOwners = SavedEventMemorySceneOwners(state) | liveOwners;
        List<NativeEventMemoryRegion> regions = [.. NativeEventMemoryOwnership.RootRegions(state), .. NativeEventMemoryOwnership.SceneRegions(sceneOwners)];
        NativeEventMemoryOwnership.Validate(memory, regions);
    }
}
