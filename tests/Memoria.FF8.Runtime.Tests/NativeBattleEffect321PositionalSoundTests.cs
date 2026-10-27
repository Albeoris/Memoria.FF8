using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect321PositionalSoundTests
{
    [Fact]
    public void Unknown_W_is_transported_without_becoming_a_numeric_operand_and_both_register_owners_are_published()
    {
        NativeBattleEffectMemory memory = Seed();
        NativeBattleEffectArena point = Point(1, -2, 1024);
        NativeBattleEffect321SoundProjection result = NativeBattleEffect321PositionalSound.Project5011B0(memory, View(), Projection(), point);
        Assert.Equal(new[] { true, true, true, true, true, true, false, false }, result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value);
        Assert.Equal(memory.GeometryRegisters!.Sxy[1], result.Memory.GeometryRegisters!.Sxy[0]);
        Assert.Equal(memory.GeometryRegisters.Sz[1], result.Memory.GeometryRegisters.Sz[0]);
        Assert.Equal(1024u, result.Memory.GeometryRegisters.Sz[3]);
        Assert.Equal(1024, result.Memory.ScriptedColorRegisters.Mac[2]);
        Assert.Equal(memory.GeometryRegisters.Otz, result.Memory.GeometryRegisters.Otz);
        Assert.Equal(result.Memory.GeometryRegisters.Sxy[2], BinaryPrimitives.ReadUInt32LittleEndian(result.Output12.Read(0, 4)));
        Assert.Equal(result.Memory.ScriptedColorRegisters.Ir0, BinaryPrimitives.ReadUInt32LittleEndian(result.Output12.Read(4, 4)));
        Assert.Equal(result.Memory.ScriptedColorRegisters.Flags, BinaryPrimitives.ReadUInt32LittleEndian(result.Output12.Read(8, 4)));
        NativeBattleEffect321SoundProjection known = NativeBattleEffect321PositionalSound.Project5011B0(memory, View(), Projection(), point.Write(6, [0xFE, 0x7F]));
        Assert.Equal(result.Output12.Read(0, 12), known.Output12.Read(0, 12));
        Assert.Equal(result.Pan, known.Pan);
        Assert.Equal((byte)0xFE, known.Memory.ScriptedColorRegisters!.NormalInput!.Value[6]);
    }

    [Theory]
    [InlineData(-32768, 80)]
    [InlineData(-1, 128)]
    [InlineData(0, 128)]
    [InlineData(1, 128)]
    [InlineData(32767, 175)]
    public void Pan_uses_signed_screen_WORD_truncation_and_native_clamps(short x, byte expected)
    {
        NativeBattleEffect321SoundProjection result = NativeBattleEffect321PositionalSound.Project5011B0(Seed(), View(), Projection(), Point(x, 0, 1024));
        Assert.Equal(expected, result.Pan);
    }

    [Fact]
    public void Partial_W_knownness_and_next_projection_survive_JSON_while_unknown_XYZ_fails()
    {
        NativeBattleEffectArena point = Point(3, 4, 1024).Write(6, [0xAD]);
        NativeBattleEffect321SoundProjection first = NativeBattleEffect321PositionalSound.Project5011B0(Seed(), View(), Projection(), point);
        Assert.True(first.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(first.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first.Memory)))!);
        NativeBattleEffect321SoundProjection expected = NativeBattleEffect321PositionalSound.Project5011B0(first.Memory, View(), Projection(), point);
        NativeBattleEffect321SoundProjection actual = NativeBattleEffect321PositionalSound.Project5011B0(restored, View(), Projection(), point);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect321PositionalSound.Project5011B0(Seed(), View(), Projection(), point.MarkUnknown(4, 1)));
    }

    private static NativeBattleEffectMemory Seed() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sxy = [11, 22, 33], Sz = [1, 2, 3, 4], Otz = 0x1234 }, ScriptedColorRegisters = new([0, 0, 0], [0, 0, 0], [0, 0, 0], 0), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Dqa = 1, Dqb = 8192 } };
    private static NativeBattleCameraProjection Projection() => new(null, 128, 20, 0);
    private static NativeBattlePackedMatrix32 View()
    {
        byte[] bytes = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 4096);
        return new(bytes.ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray());
    }
    private static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
}
