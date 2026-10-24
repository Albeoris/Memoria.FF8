using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private static IReadOnlyList<NativeWorldShadowDraw> CaptureShadows(WorldState state, WorldRenderFrame frame)
    {
        List<NativeWorldShadowDraw> result = [];
        if (frame.AvatarVisible && state.NativeFootRuntime is { } foot)
        {
            byte shape = foot.EntityType switch
            {
                0 => 0,
                1 => 4,
                2 => 5,
                _ => throw new NotSupportedException("World shadow requires the controlled actor's original shape.")
            };
            result.Add(new(shape, frame.Position, checked((short)state.BodyYaw), -unchecked((short)(state.NativeHeight - (state.GroundType is <= 5 ? 320 : 0)))));
        }
        else if (frame.AvatarVisible && state.NativeRagnarokRuntime is { } ship)
        {
            short ground = ship.CurrentGroundHeight ?? throw new NotSupportedException("Ragnarok shadow requires its retained selected ground height.");
            result.Add(new(1, frame.Position, checked((short)state.BodyYaw), -ground));
        }
        foreach (NativeWorldStaticModelDraw model in frame.StaticModels ?? [])
        {
            byte shape = model.Model.ShapeId;
            int entity = NativeTrainWorldObject.ClassifyShape(shape);
            if (entity is not (>= 0 and < 10 or 128 or >= 32 and <= 40 or 132) && shape is not (1 or 3 or 64))
            {
                continue;
            }
            short correction = WorldStaticModelTransform.AllocationAngles(shape, 0, 0, 0).Yaw;
            short yaw = unchecked((short)(model.Pose.Angles.Yaw - correction));
            float ground = model.Pose.Anchor.Y;
            float scale = 1;
            if (model.ObjectId is { Kind: WorldRenderObjectKind.Controlled } && state.NativeCarRuntime is not null)
            {
                ground = -unchecked((short)(state.NativeHeight - (state.GroundType is <= 5 ? 320 : 0)));
            }
            if (state.NativeGardenRuntime is { } garden && model.ObjectId is { Kind: WorldRenderObjectKind.Allocation } id && id.Index == garden.Allocation.Garden64Index)
            {
                scale = garden.Objects[id.Index].Position.Y > 64 ? 0.75f : 1;
                if (state.NativeFootRuntime is null)
                {
                    ground = -(garden.CurrentGroundHeight ?? throw new NotSupportedException("Garden shadow requires its retained selected ground height."));
                }
            }
            result.Add(new(shape, model.Pose.Anchor, yaw, ground, scale));
        }
        foreach (NativeWorldAnimatedModelDraw model in frame.AnimatedModels ?? [])
        {
            if (state.NativeRagnarokRuntime is { } ship && model.ObjectId is { Kind: WorldRenderObjectKind.Allocation } id && id.Index == ship.Allocation.RagnarokIndex)
            {
                result.Add(new(1, model.Position, unchecked((short)MathF.Round(model.Heading * 4096 / MathF.Tau)), model.Position.Y));
            }
        }
        return result.AsReadOnly();
    }
}
