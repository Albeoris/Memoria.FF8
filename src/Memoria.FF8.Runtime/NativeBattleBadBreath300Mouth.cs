using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath300MouthStep(NativeBattleEffectMemory Memory, NativeBattleGraphicsModel Model, NativeBattleVector Position);

public static class NativeBattleBadBreath300Mouth
{
    public static NativeBattleBadBreath300MouthStep Prepare(NativeBattleEffectMemory memory, NativeBattleGraphicsModel model, NativeBattlePoseSource pose, ImmutableArray<byte> mouthAnchor)
    {
        RequireAnchor(mouthAnchor);
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, pose);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
        int bone = BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan().Slice(6));
        if (bone < 0 || bone >= bones.Length)
        {
            throw new InvalidDataException("Bad Breath mouth anchor leaves its actual body bone publication.");
        }
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Bad Breath mouth requires its actual model world matrix.");
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, bones[bone]);
        (NativeBattleEffectMemory updated, NativeBattleVector position) = TransformComposed(memory, composed, mouthAnchor);
        return new(updated, model, position);
    }

    public static (NativeBattleEffectMemory Memory, NativeBattleVector Position) TransformComposed(NativeBattleEffectMemory memory, NativeBattleActionMatrix matrix, ImmutableArray<byte> mouthAnchor) => NativeBattleComposedAnchorTransform.Transform(memory, matrix, mouthAnchor);

    private static void RequireAnchor(ImmutableArray<byte> anchor)
    {
        if (anchor.IsDefault || anchor.Length != 8)
        {
            throw new InvalidDataException("Bad Breath mouth requires its original packed eight-byte anchor.");
        }
    }
}
