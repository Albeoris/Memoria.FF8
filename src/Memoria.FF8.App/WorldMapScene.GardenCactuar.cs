using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState StepGardenCactuar(WorldState state, out WorldFieldSelection? fieldEntry)
    {
        fieldEntry = null;
        NativeGardenWorldRuntimeState garden = state.NativeGardenRuntime!;
        NativeCactuarWorldState? cactuar = garden.Cactuar;
        if (cactuar is null || NativeWorldEntryControl.DelayAfterCallback(state.Frame, 0) > 0 || garden.Transfer is { Counter: < 31 })
        {
            return state;
        }
        int index = garden.Allocation.CactuarIndex;
        NativeGardenWorldObject body = garden.Objects[index];
        NativeWorldForestState forest = state.NativeFootRuntime?.Forest ?? garden.Forest;
        if (state.NativeFootRuntime is not null && cactuar.Phase is 1 or 2 or 3 && GardenProjection(state).IsNear(body.Position))
        {
            NativeWorldPathProjectionCertificate projection = GardenProjection(state);
            NativeWorldPathWords player = projection.RelativeWords(new(state.NativeGardenRuntime!.ProjectionCamera!.X, state.NativeGardenRuntime.ProjectionCamera.NativeHeight, -state.NativeGardenRuntime.ProjectionCamera.RawZ));
            NativeWorldPathWords target = projection.RelativeWords(body.Position);
            int x = unchecked((short)(player.X - target.X));
            int z = unchecked((short)(player.Z - target.Z));
            if (unchecked((uint)(x * x + z * z)) < 40000)
            {
                if (assets.FieldEntries.Entries.Count <= 712)
                {
                    throw new InvalidDataException("Native Cactuar field entry712 is absent.");
                }
                fieldEntry = new WorldFieldSelection(-1, -1, -1, assets.FieldEntries.Entries[712], []);
                return state;
            }
        }
        bool accepted = false;
        if (cactuar.Phase == 0)
        {
            forest = forest.DrawRandom(assets.ForestTables.Random, out byte x);
            forest = forest.DrawRandom(assets.ForestTables.Random, out byte z);
            NativeWorldPathPosition seed = garden.Allocation.CactuarLoadPosition;
            NativeWorldPathPosition proposed = seed with { X = unchecked(seed.X + 16 * x), Z = unchecked(seed.Z - 16 * z) };
            WorldGroundSelection? selected = null;
            bool? admission = null;
            foreach (NativeWorldGroundAlternative alternative in garden.GroundCache.Alternatives)
            {
                WorldGroundSelection? candidate = assets.LookupNativeGround(proposed.X, -proposed.Z, replacements, alternative.Entries);
                bool admit = candidate is not null && candidate.Identity != alternative.CurrentGround && candidate.Ground.Triangle.GroundType == 8;
                if (admission is bool prior && (prior != admit || admit && selected != candidate))
                {
                    throw new NotSupportedException("Native Cactuar proposal differs across retained ground-cache alternatives.");
                }
                admission = admit;
                selected = candidate;
            }
            accepted = admission!.Value;
            if (accepted)
            {
                body = body with { Position = proposed with { Y = -(int)selected!.Ground.Height }, RotationX = 0, RotationY = 0, RotationZ = 0 };
            }
        }
        cactuar = cactuar.Advance(accepted, out bool emit);
        if (emit)
        {
            forest = forest.EmitCactuarBurst(body.Position, assets.ForestTables.Random, assets.ForestTables.Effects);
        }
        garden = garden with { Cactuar = cactuar, Forest = forest, Objects = garden.Objects.SetItem(index, body) };
        NativeFootWorldRuntimeState? foot = state.NativeFootRuntime;
        if (foot is not null)
        {
            foot = foot with { Forest = forest };
        }
        return state with { NativeGardenRuntime = garden, NativeFootRuntime = foot };
    }
}
