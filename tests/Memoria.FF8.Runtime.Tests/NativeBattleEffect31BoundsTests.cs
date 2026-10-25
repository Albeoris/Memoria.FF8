using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect31BoundsTests
{
    [Fact]
    public void Full_MAC_comparison_precedes_low_WORD_store_and_copies_retained_padding()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleFileStaging staging = Staging().Write(NativeBattleTemporaryMemory.Address + 6, [0x5A, 0xA5]).Write(NativeBattleTemporaryMemory.Address + 0x1E, [0x34, 0x12]).Write(NativeBattleTemporaryMemory.Address + 0x26, [0xCD, 0xAB]);
        NativeBattleEffect31BoundsStep step = NativeBattleEffect31Bounds.Advance(memory, staging, Task(), Source(2, short.MinValue), [Matrix(30000, 0, 0, 0), Matrix(40000, -40000, 50000, 0xBEEF)]);
        Assert.Equal(30000, step.Task.ReadWord(0x48));
        Assert.Equal(unchecked((ushort)40000), step.Task.ReadWord(0x50));
        Assert.Equal(unchecked((ushort)-40000), step.Task.ReadWord(0x4A));
        Assert.Equal(0, step.Task.ReadWord(0x52));
        Assert.Equal(0, step.Task.ReadWord(0x4C));
        Assert.Equal(17232, step.Task.ReadWord(0x54));
        Assert.Equal(0x1234, step.Task.ReadWord(0x4E));
        Assert.Equal(0xABCD, step.Task.ReadWord(0x56));
        Assert.Equal(NativeBattleTemporaryMemory.Address, step.TemporaryAddress);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(new byte[] { 0x00, 0x80, 0x5A, 0xA5 }, step.Staging.Read(step.TemporaryAddress + 4, 4));
        Assert.Equal(40000, BinaryPrimitives.ReadInt32LittleEndian(step.Staging.Read(step.TemporaryAddress + 8, 4)));
        Assert.Equal(-40000, BinaryPrimitives.ReadInt32LittleEndian(step.Staging.Read(step.TemporaryAddress + 12, 4)));
        Assert.Equal(17232, BinaryPrimitives.ReadInt32LittleEndian(step.Staging.Read(step.TemporaryAddress + 16, 4)));
        Assert.Equal(new int?[] { 40000, -40000, 17232 }, step.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { short.MaxValue, short.MinValue, 17232 }, step.Memory.ScriptedColorRegisters.Ir);
        Assert.Equal((byte)0xEF, step.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal((byte)0xBE, step.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(new byte[] { 0x5A, 0xA5 }, step.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6, 2).ToArray());
        Assert.Equal(step.Memory.ScriptedColorRegisters.Flags, BinaryPrimitives.ReadUInt32LittleEndian(step.Staging.Read(step.TemporaryAddress + 0x28, 4)));
    }

    [Fact]
    public void Wrapped_minimum_is_used_by_the_next_full_width_branch_without_repairing_order()
    {
        NativeBattleEffect31BoundsStep step = NativeBattleEffect31Bounds.Advance(NativeBattleEffectMemory.FromColdProcess(), Staging(), Task(), Source(3), [Matrix(0, 0, 0, 0), Matrix(-40000, 0, 0, 0), Matrix(10000, 0, 0, 0)]);
        Assert.Equal(10000, step.Task.ReadWord(0x48));
        Assert.Equal(0, step.Task.ReadWord(0x50));
        Assert.True(step.Task.ReadWord(0x48) > step.Task.ReadWord(0x50));
    }

    [Fact]
    public void One_bone_reads_only_translation_WORDs_and_does_not_touch_GTE_or_retained_transform_bytes()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattlePackedMatrix32 first = Matrix(0x12345678, -1, 0x76543210, 0);
        bool[] known = new bool[32];
        foreach (int at in new[] { 20, 24, 28 })
        {
            known[at] = true;
            known[at + 1] = true;
        }
        first = first with { Known = known.ToImmutableArray() };
        NativeBattleFileStaging staging = Staging().Write(NativeBattleTemporaryMemory.Address + 4, [0xF1, 0xE2, 0xD3, 0xC4]).Write(NativeBattleTemporaryMemory.Address + 8, BitConverter.GetBytes(0xA1B2C3D4u)).Write(NativeBattleTemporaryMemory.Address + 0x28, BitConverter.GetBytes(0x98765432u));
        NativeBattleEffect31BoundsStep step = NativeBattleEffect31Bounds.Advance(memory, staging, Task(), Source(1), [first]);
        Assert.Same(memory, step.Memory);
        Assert.Equal(0x5678, step.Task.ReadWord(0x48));
        Assert.Equal(ushort.MaxValue, step.Task.ReadWord(0x4A));
        Assert.Equal(0x3210, step.Task.ReadWord(0x4C));
        Assert.Equal(step.Task.ReadWord(0x48), step.Task.ReadWord(0x50));
        Assert.Equal(step.Task.ReadWord(0x4A), step.Task.ReadWord(0x52));
        Assert.Equal(step.Task.ReadWord(0x4C), step.Task.ReadWord(0x54));
        Assert.Equal(new byte[] { 0xF1, 0xE2, 0xD3, 0xC4 }, step.Staging.Read(step.TemporaryAddress + 4, 4));
        Assert.Equal(0xA1B2C3D4u, BinaryPrimitives.ReadUInt32LittleEndian(step.Staging.Read(step.TemporaryAddress + 8, 4)));
        Assert.Equal(0x98765432u, BinaryPrimitives.ReadUInt32LittleEndian(step.Staging.Read(step.TemporaryAddress + 0x28, 4)));
    }

    [Fact]
    public void Unknown_copied_padding_and_vector_W_survive_json_without_becoming_zero()
    {
        NativeBattleEffect31BoundsStep step = NativeBattleEffect31Bounds.Advance(NativeBattleEffectMemory.FromColdProcess(), Staging(), Task(), Source(2), [Matrix(0, 0, 0, 0), Matrix(1, 2, 3, null)]);
        NativeBattleEffect31BoundsStep restored = JsonSerializer.Deserialize<NativeBattleEffect31BoundsStep>(JsonSerializer.Serialize(step))!;
        Assert.Equal(0, restored.Task.ReadWord(0x48));
        Assert.Equal(1, restored.Task.ReadWord(0x50));
        Assert.Throws<NotSupportedException>(() => restored.Task.ReadWord(0x4E));
        Assert.Throws<NotSupportedException>(() => restored.Task.ReadWord(0x56));
        Assert.Equal(new bool[] { false, false }, restored.Staging.ReadSnapshot(restored.TemporaryAddress + 6, 2).Known);
        Assert.False(restored.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(restored.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Null(restored.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(restored.Memory.GeometryRegisters.Rotation20[19]);
    }

    private static NativeBattleFileStaging Staging() => NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
    private static NativeBattleModulePoolTask Task() => NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 0x58, 1).Slots[0];
    private static NativeBattlePackedMatrix32 Matrix(int x, int y, int z, ushort? padding) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Y = y, Z = z }, padding);
    private static NativeBattlePoseSource Source(int count, short length = 0)
    {
        byte[] skeleton = new byte[16 + count * 48];
        skeleton[0] = checked((byte)count);
        for (int bone = 0; bone < count; bone++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16 + bone * 48), bone == 0 ? (short)-1 : (short)0);
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18 + bone * 48), length);
        }
        return new(skeleton.ToImmutableArray(), [0]);
    }
}
