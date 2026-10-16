using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState StepFootType2Cactuar(WorldState state, out WorldFieldSelection? fieldEntry)
    {
        fieldEntry = null;
        NativeFootWorldRuntimeState foot = state.NativeFootRuntime!;
        NativeNormalFootObjectProcess objects = foot.NormalObjects!;
        NativeCactuarWorldState? cactuar = objects.Cactuar;
        if (cactuar is null || NativeWorldEntryControl.DelayAfterCallback(state.Frame, 0) > 0)
        {
            return state;
        }
        NativeGardenWorldObject body = objects.CactuarBody!;
        NativeWorldForestState forest = foot.Forest!;
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(objects.Camera);
        if (cactuar.Phase is 1 or 2 or 3 && projection.IsNear(body.Position))
        {
            NativeWorldPathWords player = projection.RelativeWords(objects.Camera.Origin);
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
            NativeWorldPathPosition seed = objects.CactuarLoadPosition!.Value;
            NativeWorldPathPosition proposed = seed with { X = unchecked(seed.X + 16 * x), Z = unchecked(seed.Z - 16 * z) };
            WorldGroundSelection? selected = null;
            bool? admission = null;
            foreach (NativeWorldGroundAlternative alternative in foot.GroundCache!.Alternatives)
            {
                WorldGroundSelection? candidate = assets.LookupNativeGround(proposed.X, -proposed.Z, replacements, alternative.Entries);
                bool admit = candidate is not null && candidate.Identity != alternative.CurrentGround && candidate.Ground.Triangle.GroundType == 8;
                if (admission is bool prior && (prior != admit || admit && selected != candidate))
                {
                    throw new NotSupportedException("Native type2 Cactuar proposal differs across retained ground-cache alternatives.");
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
        return state with { NativeFootRuntime = foot with { Forest = forest, NormalObjects = objects with { Cactuar = cactuar, CactuarBody = body } } };
    }

    private NativeWorldFootType2AnimationResult StepFootType2Draw(WorldState before, NativeFootWorldRuntimeState foot, int bodyYaw, int cameraYaw, int targetX, int targetZ, int targetHeight, bool moving)
    {
        NativeWorldForestState forest = foot.Forest!;
        if (before.GroundType <= 5)
        {
            return new(before.AnimationId, checked((ushort)before.AnimationCounter), before.AnimationCounter, forest);
        }
        WorldState cameraState = before with { BodyYaw = bodyYaw, CameraYaw = cameraYaw, CameraTargetX = targetX, CameraTargetZ = targetZ, CameraTargetNativeHeight = targetHeight, NativeFootRuntime = foot };
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(NativeWorldFootCameraState.Create(cameraState));
        NativeWorldPathNodeProjection draw = projection.ProjectNode(new(before.X, before.NativeHeight, -before.Z)) ?? throw new NotSupportedException(projection.UnsupportedReason);
        if (!draw.Visible)
        {
            return new(before.AnimationId, checked((ushort)before.AnimationCounter), before.AnimationCounter, forest);
        }
        int loadedLength = assets.GetFootModel(2).Animations[before.AnimationId].Frames.Count * 16;
        return NativeWorldFootType2Animation.Step(before.AnimationId, checked((ushort)before.AnimationCounter), loadedLength, moving, foot.NormalObjects!.ForcedAnimationC76628, forest, assets.ForestTables.Random);
    }
}
