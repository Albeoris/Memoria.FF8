using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSaliva138Mouth
{
    public static (NativeBattleEffectMemory Memory, NativeBattleVector Position) Prepare(NativeBattleEffectMemory memory, NativeBattleGraphicsModel model, NativeBattlePoseSource pose, ImmutableArray<byte> anchor)
    {
        if (anchor.IsDefault || anchor.Length != 8)
        {
            throw new InvalidDataException("Saliva requires its original packed bone anchor.");
        }
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
        int bone = BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan().Slice(6));
        if (bone < 0 || bone >= bones.Length)
        {
            throw new InvalidDataException("Saliva anchor leaves its actual current body bone publication.");
        }
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Saliva requires its actual current model world matrix.");
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, bones[bone]);
        return NativeBattleComposedAnchorTransform.Transform(memory, composed, anchor);
    }
}
