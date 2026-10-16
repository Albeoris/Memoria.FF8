using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState StepRagnarokCactuar(WorldState state, out WorldFieldSelection? fieldEntry)
    {
        fieldEntry = null;
        NativeRagnarokWorldRuntimeState runtime = state.NativeRagnarokRuntime!;
        NativeCactuarWorldState? cactuar = runtime.Cactuar;
        if (cactuar is null || runtime.Transfer is not null || NativeWorldEntryControl.DelayAfterCallback(state.Frame, 0) > 0)
        {
            return state;
        }
        int index = runtime.Objects.Select((item, index) => (item, index)).Single(pair => pair.item.ShapeId == 94).index;
        NativeRagnarokWorldObject body = runtime.Objects[index];
        NativeWorldForestState forest = state.NativeFootRuntime?.Forest ?? runtime.Forest ?? throw new NotSupportedException("Ragnarok Cactuar requires the retained original shared random/particle owner.");
        if (state.NativeFootRuntime is not null && cactuar.Phase is 1 or 2 or 3)
        {
            NativeWorldPathProjectionCertificate projection = RagnarokProjection(state);
            if (projection.IsNear(body.Position))
            {
                NativeWorldFootCameraState camera = runtime.ProjectionCamera!;
                NativeWorldPathWords player = projection.RelativeWords(new(camera.X, camera.NativeHeight, -camera.RawZ));
                NativeWorldPathWords target = projection.RelativeWords(body.Position);
                int x = unchecked((short)(player.X - target.X));
                int z = unchecked((short)(player.Z - target.Z));
                if (unchecked((uint)(x * x + z * z)) < 40000)
                {
                    if (assets.FieldEntries.Entries.Count <= 712)
                    {
                        throw new InvalidDataException("Original Cactuar field packet712 is absent.");
                    }
                    fieldEntry = new(-1, -1, -1, assets.FieldEntries.Entries[712], []);
                    return state;
                }
            }
        }
        bool accepted = false;
        if (cactuar.Phase == 0)
        {
            forest = forest.DrawRandom(assets.ForestTables.Random, out byte x);
            forest = forest.DrawRandom(assets.ForestTables.Random, out byte z);
            NativeWorldPathPosition seed = runtime.Allocation.Objects[index].Position;
            NativeWorldPathPosition proposed = seed with { X = unchecked(seed.X + 16 * x), Z = unchecked(seed.Z - 16 * z) };
            WorldGroundSelection? selected = null;
            bool? admission = null;
            foreach (NativeWorldGroundAlternative alternative in runtime.GroundCache!.Alternatives)
            {
                WorldGroundSelection? candidate = assets.LookupNativeGround(proposed.X, -proposed.Z, replacements, alternative.Entries);
                bool admit = candidate is not null && candidate.Identity != alternative.CurrentGround && candidate.Ground.Triangle.GroundType == 8;
                if (admission is bool prior && (prior != admit || admit && selected != candidate))
                {
                    throw new NotSupportedException("Ragnarok Cactuar proposal differs across actual retained ground alternatives.");
                }
                admission = admit;
                selected = candidate;
            }
            accepted = admission!.Value;
            if (accepted)
            {
                body = body with { Position = proposed with { Y = -(int)selected!.Ground.Height }, Pitch = 0, Yaw = 0, Roll = 0 };
            }
        }
        cactuar = cactuar.Advance(accepted, out bool emit);
        if (emit)
        {
            forest = forest.EmitCactuarBurst(body.Position, assets.ForestTables.Random, assets.ForestTables.Effects);
        }
        runtime = runtime with { Cactuar = cactuar, Forest = forest, Objects = runtime.Objects.SetItem(index, body) };
        NativeFootWorldRuntimeState? foot = state.NativeFootRuntime;
        if (foot is not null)
        {
            foot = foot with { Forest = forest };
        }
        return state with { NativeRagnarokRuntime = runtime, NativeFootRuntime = foot };
    }
}
