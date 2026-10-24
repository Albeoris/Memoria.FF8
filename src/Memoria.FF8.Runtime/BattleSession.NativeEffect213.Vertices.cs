using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect213CountStep CountNativeEffect213Vertices(NativeBattleGraphicsState graphics, NativeBattleEffect213Frame frame, uint modelAddress)
    {
        int actor = NativeEffect213Actor(graphics, modelAddress);
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect213 vertex counting requires its current addressed memory.");
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect213 vertex counting requires its current primary mask.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 56);
        NativeBattleEffectArena temporary = NativeDeformedBodyView(allocation.Staging, allocation.Address, 56).Write(0x20, BitConverter.GetBytes(mask));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 56);
        int count = NativeBattleStorm11Vertices.Count69EEE0(resource.Geometry ?? throw new NotSupportedException("Effect213 vertex counting requires its current primary geometry."), mask);
        if (model.LinkedTransform is not null)
        {
            temporary = temporary.Write(0x20, BitConverter.GetBytes(uint.MaxValue));
            staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 56);
            count = unchecked(count + NativeBattleStorm11Vertices.Count69EEE0(resource.LinkedGeometry ?? throw new NotSupportedException("Effect213 vertex counting requires its current linked geometry."), uint.MaxValue));
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 56);
        NativeFileStaging = staging;
        return new(frame with { Staging = staging }, count);
    }

    private NativeBattleEffect213PointStep SelectNativeEffect213Vertex(ref NativeBattleGraphicsState graphics, NativeBattleEffect213Frame frame, uint modelAddress, int ordinal, NativeBattleEffectArena retained8)
    {
        byte actor = checked((byte)NativeEffect213Actor(graphics, modelAddress));
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect213 vertex selection requires its current addressed memory.");
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Effect213 vertex selection requires its current primary skeleton.");
        NativeDeformedBodyBridge body = AllocateNativeDeformedBody(frame.Memory, frame.Effect, frame.Random, frame.Staging!, graphics, actor, 56);
        NativeBattleActionMatrix world = body.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect213 vertex selection requires its current world matrix.");
        uint mask = body.Model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect213 vertex selection requires its current primary mask.");
        body = body with { Temporary = body.Temporary.Write(0x20, BitConverter.GetBytes(mask)).Write(0x24, BitConverter.GetBytes(ordinal)), Model = NativeBattleBodyBoneMatrices.Compose5095B0(body.Model, world) };
        (body, bool found) = Select(body, resource.Geometry ?? throw new NotSupportedException("Effect213 vertex selection requires its current primary geometry."), NativeBattleBodyBoneMatrices.Require(body.Model, source));
        body = RestoreNativeDeformedBody(body, source, false);
        if (!found && body.Model.LinkedTransform is not null)
        {
            NativeBattlePoseSource linked = resource.LinkedPoseSource ?? throw new NotSupportedException("Effect213 vertex selection requires its current linked skeleton.");
            body = body with { Temporary = body.Temporary.Write(0x20, BitConverter.GetBytes(uint.MaxValue)) };
            var composed = ComposeNativeDeformedLinked(body, linked, world);
            (body, _) = Select(composed.Bridge, resource.LinkedGeometry ?? throw new NotSupportedException("Effect213 vertex selection requires its current linked geometry."), composed.Matrices);
            body = RestoreNativeDeformedBody(body, linked, true);
        }
        retained8 = NativeBattleEffectNodeContextMemory.Copy(body.Temporary, 0x28, retained8, 0, 8);
        body = FinishNativeDeformedBody(body, actor, 56);
        graphics = body.Graphics;
        NativeFileStaging = body.Staging;
        return new(frame with { Memory = body.Memory, Staging = body.Staging }, retained8);

        static (NativeDeformedBodyBridge Body, bool Found) Select(NativeDeformedBodyBridge current, ImmutableArray<NativeBattleRawMesh> geometry, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            NativeBattleEffect213PosedVertexStep selected = NativeBattleEffect213PosedVertex.Select6CE880(current.Memory, current.Temporary, geometry, matrices);
            current = current with { Memory = selected.Memory, Temporary = selected.Temporary56, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, current.TemporaryAddress, selected.Temporary56, 56) };
            return (current, selected.Found);
        }
    }
}
