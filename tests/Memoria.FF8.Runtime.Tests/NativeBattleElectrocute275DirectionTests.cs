using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectrocute275DirectionTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 4096)]
    [InlineData(0, 1024, -4096, 0, 0)]
    [InlineData(1024, 0, 0, -4096, 0)]
    [InlineData(-1024, 0, 0, 4096, 0)]
    [InlineData(512, 512, -2048, -2896, 2047)]
    [InlineData(-512, -512, 2047, 2896, 2047)]
    public void Native_angles_keep_NEG_before_arithmetic_shift(short pitch, short yaw, short x, short y, short z)
    {
        Assert.Equal(new NativeBattleVector(x, y, z), NativeBattleElectrocute275Direction.Compute(pitch, yaw));
    }
}

public sealed class NativeBattleElectrocute275BoneProjectionTests
{
    [Fact]
    public void Current_bone_IR_WORD_then_wrapped_offsets_project_to_full_quarter_depth_DWORD()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096), null);
        NativeBattleEffectMemory result = Project(memory, bone);
        Assert.Equal(-32759, unchecked((short)BinaryPrimitives.ReadInt16LittleEndian(result.ScriptedColorRegisters!.NormalInput!.Value.AsSpan())));
        Assert.False(result.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.False(result.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal(575u, U32(result.Arena!, Offset + 0x14));
        Assert.Equal(2300u, result.GeometryRegisters!.Sz[3]);
        Assert.Equal(200, result.GeometryRegisters.Translation[2]);
        Assert.Null(result.GeometryRegisters.Rotation20[18]);
        Assert.False(result.Arena!.ReadSnapshot(Offset + 0x1C, 4).Known[0]);
        Assert.Equal((short)10, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(Offset + 4, 2)));
    }

    [Fact]
    public void Linked_records_have_one_arena_owner_and_JSON_next_keeps_current_registers()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = Record(memory.Arena!, Offset + 40, 0).Write(Offset + 0x24, BitConverter.GetBytes(Address + 40)) };
        NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        NativeBattleEffectMemory projected = Project(memory, bone);
        Assert.Equal(575u, U32(projected.Arena!, Offset + 40 + 0x14));
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(projected)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Project(projected, bone))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Project(restored, bone))));
    }

    [Fact]
    public void Invalid_chain_bone_and_source_bytes_fail_without_replacing_current_owners()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        Assert.Throws<InvalidDataException>(() => Project(memory with { Arena = memory.Arena!.Write(Offset + 0x24, BitConverter.GetBytes(Address)) }, bone));
        Assert.Throws<InvalidDataException>(() => Project(memory with { Arena = memory.Arena!.Write(Offset + 0x24, BitConverter.GetBytes(Address + 1)) }, bone));
        Assert.Throws<InvalidDataException>(() => Project(memory with { Arena = memory.Arena!.Write(Offset + 15, [255]) }, bone));
        Assert.Throws<NotSupportedException>(() => Project(memory with { GeometryRegisters = null }, bone));
    }

    private const int Offset = 0x72D4;
    private const uint Address = NativeBattleEffectNodeMemory.ArenaAddress + Offset;
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = Record(NativeBattleEffectArena.Clear().MarkUnknown(Offset, 80), Offset, 0), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Record(NativeBattleEffectArena arena, int offset, uint next) => arena.Write(offset, BitConverter.GetBytes((short)3)).Write(offset + 4, BitConverter.GetBytes((short)10)).Write(offset + 6, BitConverter.GetBytes((short)20)).Write(offset + 8, BitConverter.GetBytes((short)100)).Write(offset + 15, [2]).Write(offset + 0x24, BitConverter.GetBytes(next));
    private static NativeBattleEffectMemory Project(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 bone) => NativeBattleElectrocute275BoneProjection.ProjectChain(memory, Address, Address, 80, index => index == 2 ? bone : throw new InvalidDataException("Actual bone selector differs."), Read, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 200 }, null), new(null, 100, 160, 108), memory.ScriptedGraphics!);
    private static byte[] Read(int offset, int length)
    {
        Assert.Equal(6, offset);
        Assert.Equal(6, length);
        byte[] xyz = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(xyz, 10000);
        BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), 40);
        return xyz;
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
