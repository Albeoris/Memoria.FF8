using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static int ActorNativeEffect65(uint pointer, NativeBattleGraphicsState graphics)
    {
        if (!NativeBattleGraphicsModelAddress.TryDecode(pointer, out int actorIndex, graphics.Models.Length))
        {
            throw new InvalidDataException("Effect65 requires an actual physical model row.");
        }
        return actorIndex;
    }

    private NativeBattlePoseSource PoseNativeEffect65(NativeBattleGraphicsState graphics, NativeBattleEffect65Frame frame, uint pointer)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect65 requires its current resource heap.");
        return NativeGraphicsPoseResource(graphics, ActorNativeEffect65(pointer, graphics)).PoseSource ?? throw new NotSupportedException("Effect65 requires its current skeleton.");
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect65PointOutputStep Step) ReadNativeEffect65CasterPoint(NativeBattleGraphicsState graphics, NativeBattleEffect65Frame frame, NativeBattleEffectArena source8, NativeBattleEffectArena output, bool wide)
    {
        uint pointer = frame.State.ReadDword(0x2544F70);
        int actor = ActorNativeEffect65(pointer, graphics);
        NativeBattlePoseSource source = PoseNativeEffect65(graphics, frame, pointer);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 88);
        NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.ActorModels.Get(actor), source, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
        NativeBattleGraphicsModel model = prepared.Model;
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        int selector = BinaryPrimitives.ReadInt16LittleEndian(source8.Read(6, 2));
        if ((uint)selector >= (uint)matrices.Length)
        {
            throw new InvalidDataException("Effect65 caster point selects a bone outside its current skeleton.");
        }
        NativeBattleEffect65CasterPointStep transformed = NativeBattleEffect65CasterPoint.Transform(frame.Memory, model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect65 caster points require their current world matrix."), matrices[selector], source8, output, wide);
        frame = frame with { Memory = transformed.Memory };
        return (graphics with { ActorModels = graphics.ActorModels.Set(actor, model) }, new(frame, transformed.Output));
    }
}
