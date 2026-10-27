using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79BoneMatrixTests
{
    [Fact]
    public void Disabled_model_returns_minus_one_without_reading_or_changing_any_owner()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { BoneMatrixAnchor2795BC0 = null };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattlePackedMatrix32 output = Output();
        NativeBattleBoneMatrixOutputStep step = NativeBattleBoneMatrixOutput8DE8F0.Query(memory, staging, new(4, 0), null!, int.MaxValue, int.MinValue, output);
        Assert.Equal(-1, step.Result);
        Assert.Same(memory, step.Memory);
        Assert.Same(staging, step.Staging);
        Assert.Same(output, step.Output);
    }

    [Theory]
    [InlineData(1, 2048, 1000)]
    [InlineData(1, 0, 0)]
    [InlineData(0, 2048, 0)]
    [InlineData(241, 0, 2000)]
    [InlineData(1, -2048, -1000)]
    public void Enabled_query_writes_only_translation_and_uses_full_MAC_low_words(int selector, int fraction, short expectedDistance)
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleFileStaging staging = Staging();
        NativeBattlePackedMatrix32 output = Output();
        NativeBattleBoneMatrixOutputStep step = NativeBattleBoneMatrixOutput8DE8F0.Query(memory, staging, model, source, selector, fraction, output);
        Assert.Equal(0, step.Result);
        Assert.Equal(output.Bytes.AsSpan(0, 20).ToArray(), step.Output.Bytes.AsSpan(0, 20).ToArray());
        Assert.Equal(output.Known.AsSpan(0, 20).ToArray(), step.Output.Known.AsSpan(0, 20).ToArray());
        int[] mac = [70300, -70400, 32768 + expectedDistance];
        for (int axis = 0; axis < 3; axis++)
        {
            Assert.Equal(mac[axis], step.Memory.ScriptedColorRegisters!.Mac[axis]);
            Assert.Equal((int)unchecked((short)mac[axis]), BinaryPrimitives.ReadInt32LittleEndian(step.Output.Bytes.AsSpan().Slice(20 + axis * 4, 4)));
            Assert.All(step.Output.Known.AsSpan(20 + axis * 4, 4).ToArray(), value => Assert.True(value));
        }
        Assert.NotEqual(step.Memory.ScriptedColorRegisters!.Ir[0], BinaryPrimitives.ReadInt32LittleEndian(step.Output.Bytes.AsSpan().Slice(20, 4)));
        Assert.Equal(new byte[] { 0x2C, 1, 0x70, 0xFE }, step.Memory.BoneMatrixAnchor2795BC0!.Value.AsSpan(0, 4).ToArray());
        Assert.Equal(expectedDistance, BinaryPrimitives.ReadInt16LittleEndian(step.Memory.BoneMatrixAnchor2795BC0.Value.AsSpan().Slice(4, 2)));
        Assert.Equal(new byte[] { 0xEF, 0xBE }, step.Memory.BoneMatrixAnchor2795BC0.Value.AsSpan(6, 2).ToArray());
        Assert.Equal((uint)NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(new byte[] { 0x2C, 1, 0x70, 0xFE, 0x34, 0x12, 0xEF, 0xBE }, memory.BoneMatrixAnchor2795BC0!.Value.ToArray());
    }

    [Fact]
    public void Temporary52_retains_padding_and_gap_but_publishes_matrix_MAC_and_flags_before_release()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source);
        NativeBattleFileStaging staging = Staging();
        NativeBattleBoneMatrixOutputStep step = NativeBattleBoneMatrixOutput8DE8F0.Query(Memory(), staging, model, source, 1, 2048, Output());
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleActionMatrix expected = NativeBattleCuragaMatrices.Identity8DD770() with { X = 70000, Y = -70000, Z = 32768 };
        NativeBattlePackedMatrix32 expectedPacked = NativeBattlePackedMatrix32.FromMatrix(expected, null);
        Assert.Equal(expectedPacked.Bytes.AsSpan(0, 18).ToArray(), step.Staging.Read(address, 18));
        Assert.Equal(expectedPacked.Bytes.AsSpan(20, 12).ToArray(), step.Staging.Read(address + 20, 12));
        AssertSnapshot(staging, step.Staging, address + 18, 2);
        AssertSnapshot(staging, step.Staging, address + 44, 4);
        for (int axis = 0; axis < 3; axis++)
        {
            Assert.Equal(step.Memory.ScriptedColorRegisters!.Mac[axis], BinaryPrimitives.ReadInt32LittleEndian(step.Staging.Read(address + 32u + (uint)(axis * 4), 4)));
        }
        Assert.Equal(step.Memory.ScriptedColorRegisters!.Flags, BinaryPrimitives.ReadUInt32LittleEndian(step.Staging.Read(address + 48, 4)));
        AssertSnapshot(staging, step.Staging, address + 52, 4);
        Assert.Equal(new byte[] { 0xEF, 0xBE }, step.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan(6, 2).ToArray());
    }

    [Fact]
    public void Missing_or_malformed_shared_scratch_fails_before_a_temporary_allocation()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleFileStaging staging = Staging();
        NativeBattleEffectMemory missing = Memory() with { BoneMatrixAnchor2795BC0 = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleBoneMatrixOutput8DE8F0.Query(missing, staging, Model(source), source, 1, 0, Output()));
        Assert.Throws<InvalidDataException>(() => NativeBattleBoneMatrixOutput8DE8F0.Query(missing with { BoneMatrixAnchor2795BC0 = [0, 0] }, staging, Model(source), source, 1, 0, Output()));
        Assert.Equal((uint)NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Snapshot_preserves_next_translation_scratch_and_temporary_bytes()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source);
        NativeBattleBoneMatrixOutputStep first = NativeBattleBoneMatrixOutput8DE8F0.Query(Memory(), Staging(), model, source, 241, 0, Output());
        NativeBattleBoneMatrixOutputStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleBoneMatrixOutputStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        NativeBattleBoneMatrixOutputStep expected = NativeBattleBoneMatrixOutput8DE8F0.Query(first.Memory, first.Staging, model, source, 1, -2048, first.Output);
        NativeBattleBoneMatrixOutputStep actual = NativeBattleBoneMatrixOutput8DE8F0.Query(restored.Memory, restored.Staging, model, source, 1, -2048, restored.Output);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { BoneMatrixAnchor2795BC0 = [0x2C, 1, 0x70, 0xFE, 0x34, 0x12, 0xEF, 0xBE] };

    private static NativeBattleFileStaging Staging()
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        return staging.Write(NativeBattleTemporaryMemory.Address, Enumerable.Range(0, 56).Select(index => (byte)(0x80 + index)).ToArray()).MarkUnknown(NativeBattleTemporaryMemory.Address + 19, 1).MarkUnknown(NativeBattleTemporaryMemory.Address + 45, 1);
    }

    private static NativeBattlePackedMatrix32 Output() => new(Enumerable.Range(0, 32).Select(index => (byte)(0xC0 + index)).ToImmutableArray(), Enumerable.Range(0, 32).Select(index => index % 3 != 0).ToImmutableArray());

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[16 + 2 * 48];
        skeleton[0] = 2;
        skeleton[5] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 3000);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), 2000);
        return new(skeleton.ToImmutableArray(), [1]);
    }

    private static NativeBattleGraphicsModel Model(NativeBattlePoseSource source)
    {
        NativeBattleGraphicsModel model = new(4, 3)
        {
            GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 70000, Y = -70000, Z = 32768 },
            SequenceState = new() { DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024)), new(new(0, 0, 0), new(1024, 1024, 1024))], false) }
        };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        return model with { CurrentBodyBoneMatrices = model.CurrentBodyBoneMatrices! with { Matrices = [identity, identity] } };
    }

    private static void AssertSnapshot(NativeBattleFileStaging expected, NativeBattleFileStaging actual, uint address, int length)
    {
        (byte[] expectedBytes, bool[] expectedKnown) = expected.ReadSnapshot(address, length);
        (byte[] actualBytes, bool[] actualKnown) = actual.ReadSnapshot(address, length);
        Assert.Equal(expectedBytes, actualBytes);
        Assert.Equal(expectedKnown, actualKnown);
    }
}
