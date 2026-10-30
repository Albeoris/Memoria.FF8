using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeWorldSalaryState PrepareSavedTerrainFlag(WorldState world, NativeWorldSalaryState salary, bool fieldDeparture = false)
    {
        NativeWorldGroundCacheState? cache = world.NativeFootRuntime?.GroundCache ?? world.NativeCarRuntime?.GroundCache ?? world.NativeTrainRuntime?.GroundCache ?? world.NativeScriptedRuntime?.GroundCache ?? world.NativeGardenRuntime?.GroundCache ?? world.NativeRagnarokRuntime?.GroundCache;
        if (cache is null)
        {
            return salary;
        }
        bool?[] flags = cache.Alternatives.Select(alternative => fieldDeparture ? (bool?)((assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).TextureFlags & 8) != 0) : alternative.TerrainFlag109Bit0).ToArray();
        if (!fieldDeparture && flags.All(flag => flag is null))
        {
            return salary;
        }
        bool? entrance = flags.Distinct().Count() == 1 ? flags[0] : null;
        return salary.WithTerrainEntranceFlag(entrance);
    }
}
