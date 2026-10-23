using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleOilShot136Mouth
{
    public static (NativeBattleEffectMemory Memory, NativeBattleVector Position, NativeBattleGraphicsModel Model) Prepare(NativeBattleEffectMemory memory, NativeBattleGraphicsModel model, NativeBattlePoseSource pose, ImmutableArray<byte> anchor)
    {
        if (anchor.IsDefault || anchor.Length != 8)
        {
            throw new InvalidDataException("Oil Shot requires its original packed bone anchor.");
        }
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, pose);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
        int bone = BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan().Slice(6));
        if (bone < 0 || bone >= bones.Length)
        {
            throw new InvalidDataException("Oil Shot anchor leaves its refreshed body bone publication.");
        }
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Oil Shot requires its actual current model world matrix.");
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, bones[bone]);
        var transformed = NativeBattleComposedAnchorTransform.Transform(memory, composed, anchor);
        return (transformed.Memory, transformed.Position, model);
    }
}
