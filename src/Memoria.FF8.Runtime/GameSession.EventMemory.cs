namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateEventMemoryOwners(GameState state, NativeEventMemory memory)
    {
        ValidateEventMemoryOwners(state, memory, NativeEventSceneMemoryOwners.None);
    }

    private static void ValidateEventMemoryOwners(GameState state, NativeEventMemory memory, NativeEventSceneMemoryOwners liveOwners, CapturedEventSceneProjectionCache? cache = null)
    {
        ArgumentNullException.ThrowIfNull(memory);
        IReadOnlyList<NativeEventMemoryRegion> rootRegions = NativeEventMemoryOwnership.RootRegions(state);
        NativeEventSceneMemoryOwners sceneOwners = SavedEventMemorySceneOwners(state, rootRegions, cache) | liveOwners;
        List<NativeEventMemoryRegion> regions = [.. rootRegions, .. NativeEventMemoryOwnership.SceneRegions(sceneOwners)];
        NativeEventMemoryOwnership.Validate(memory, regions);
    }
}
