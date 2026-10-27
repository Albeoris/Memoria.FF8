using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath300MouthTests
{
    [Fact]
    public void Composed_transform_loads_actual_anchor_W_and_unknown_matrix_padding_without_touching_other_owners()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = -200, Z = 300 };
        (NativeBattleEffectMemory result, NativeBattleVector position) = NativeBattleBadBreath300Mouth.TransformComposed(memory, matrix, Anchor(10, 20, -30, 9));
        Assert.Equal(new NativeBattleVector(110, -180, 270), position);
        Assert.Equal(new int?[] { 110, -180, 270 }, result.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 100, -200, 300 }, result.GeometryRegisters!.Translation);
        Assert.Equal(new byte[] { 9, 0 }, result.ScriptedColorRegisters.NormalInput!.Value.Skip(6));
        Assert.Null(result.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.GeometryRegisters.Rotation20[19]);
        Assert.Equal(memory.GeometryRegisters!.V1, result.GeometryRegisters.V1);
        Assert.Equal(memory.GeometryRegisters.Sxy, result.GeometryRegisters.Sxy);
        Assert.Same(memory.Arena, result.Arena);
        Assert.Same(memory.ScriptedLighting, result.ScriptedLighting);
    }

    [Fact]
    public void Output_is_saturated_IR_WORDs_while_full_MACs_are_retained()
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 40000, Y = -40000 };
        (NativeBattleEffectMemory memory, NativeBattleVector position) = NativeBattleBadBreath300Mouth.TransformComposed(Memory(), matrix, Anchor(1, -1, 2, 9));
        Assert.Equal(new NativeBattleVector(short.MaxValue, short.MinValue, 2), position);
        Assert.Equal(new int?[] { 40001, -40001, 2 }, memory.ScriptedColorRegisters!.Mac);
    }

    [Fact]
    public void Reached_publication_replaces_stale_bones_and_requires_actual_pose_and_signed_bone_index()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model();
        NativeBattleGraphicsModel stale = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        stale = NativeBattleBodyBoneMatrices.Compose5095B0(stale, NativeBattleCuragaMatrices.Identity8DD770() with { X = 9999 });
        NativeBattleBadBreath300MouthStep expected = NativeBattleBadBreath300Mouth.Prepare(Memory(), model, source, Anchor(1, 2, 3, 1));
        NativeBattleBadBreath300MouthStep actual = NativeBattleBadBreath300Mouth.Prepare(Memory(), stale, source, Anchor(1, 2, 3, 1));
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Model.CurrentBodyBoneMatrices), NativeBattleTestSnapshots.Json(actual.Model.CurrentBodyBoneMatrices));
        Assert.Throws<NotSupportedException>(() => NativeBattleBadBreath300Mouth.Prepare(Memory(), stale with { SequenceState = null }, source, Anchor(1, 2, 3, 1)));
        Assert.Throws<InvalidDataException>(() => NativeBattleBadBreath300Mouth.Prepare(Memory(), model, source, Anchor(1, 2, 3, -1)));
        Assert.Throws<InvalidDataException>(() => NativeBattleBadBreath300Mouth.Prepare(Memory(), model, source, Anchor(1, 2, 3, 2)));
    }

    [Fact]
    public void Unknown_register_owners_and_nonpacked_inputs_fail_and_JSON_next_retains_exact_registers()
    {
        NativeBattleEffectMemory memory = Memory();
        ImmutableArray<byte> anchor = Anchor(1, 2, 3, 9);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        Assert.Throws<NotSupportedException>(() => NativeBattleBadBreath300Mouth.TransformComposed(memory with { GeometryRegisters = null }, matrix, anchor));
        Assert.Throws<NotSupportedException>(() => NativeBattleBadBreath300Mouth.TransformComposed(memory with { ScriptedColorRegisters = null }, matrix, anchor));
        Assert.Throws<InvalidDataException>(() => NativeBattleBadBreath300Mouth.TransformComposed(memory, matrix, [1, 2, 3]));
        NativeBattleEffectMemory current = NativeBattleBadBreath300Mouth.TransformComposed(memory, matrix, anchor).Memory;
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(current))!;
        var next = NativeBattleBadBreath300Mouth.TransformComposed(current, matrix, anchor);
        var restoredNext = NativeBattleBadBreath300Mouth.TransformComposed(restored, matrix, anchor);
        Assert.Equal(next.Position, restoredNext.Position);
        Assert.Equal(NativeBattleTestSnapshots.Json(next.Memory), NativeBattleTestSnapshots.Json(restoredNext.Memory));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };

    private static ImmutableArray<byte> Anchor(short x, short y, short z, short bone)
    {
        byte[] bytes = new byte[8];
        short[] words = [x, y, z, bone];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return bytes.ToImmutableArray();
    }

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[0x70];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x10), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x12), 100);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x42), 150);
        return new(skeleton.ToImmutableArray(), [1]);
    }

    private static NativeBattleGraphicsModel Model() => new(0, 3)
    {
        GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 },
        SequenceState = new() { DecodedPose = new(new(10, 20, 30), [new(new(0, 0, 0), new(1024, 1024, 1024)), new(new(0, 128, 0), new(1024, 1024, 1024))], false) }
    };
}
