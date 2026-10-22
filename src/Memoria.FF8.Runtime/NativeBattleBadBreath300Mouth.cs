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

    public static (NativeBattleEffectMemory Memory, NativeBattleVector Position) TransformComposed(NativeBattleEffectMemory memory, NativeBattleActionMatrix matrix, ImmutableArray<byte> mouthAnchor)
    {
        RequireAnchor(mouthAnchor);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Bad Breath mouth requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Bad Breath mouth requires its current arithmetic registers."));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan().Slice(4)));
        short w = BinaryPrimitives.ReadInt16LittleEndian(mouthAnchor.AsSpan().Slice(6));
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
        registers = NativeBattleGeometry.Transform(registers);
        NativeBattleVector output = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        return (memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, output);
    }

    private static void RequireAnchor(ImmutableArray<byte> anchor)
    {
        if (anchor.IsDefault || anchor.Length != 8)
        {
            throw new InvalidDataException("Bad Breath mouth requires its original packed eight-byte anchor.");
        }
    }
}
