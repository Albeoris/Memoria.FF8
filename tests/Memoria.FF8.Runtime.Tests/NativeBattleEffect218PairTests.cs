using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218PairTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pair_FC_reaches_both_mesh_entries_and_clears_semitransparency_with_actual_fade(bool loaded)
    {
        byte[] bytes = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] points = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), points[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 0x2E112233);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60 + corner * 2), (ushort)(corner * 2));
        }
        NativeBattleStaticMeshResource mesh = new(0, 0xE21CC0, bytes.ToImmutableArray());
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena context = PairContext633090(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), Task().WriteWord(14, 1).WriteWord(32, 2560));
        NativeBattleStaticMeshContext88Step actual = loaded ? NativeBattleStaticMeshContext88.DrawLoaded(memory, mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 4122, 14) : NativeBattleStaticMeshContext88.Draw(memory, mesh, matrix, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, tableBaseBucket: 4122, depthShift: 14);
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(4122, packet.Primitive.Bucket);
        Assert.Equal(0x2C060C13u, packet.Primitive.Words[1]);
        Assert.Equal(0xFCu, Dword(actual.Context, 28));
    }

    [Theory]
    [InlineData(0, 0xF3)]
    [InlineData(1, 0xFC)]
    [InlineData(256, 0xFC)]
    public void Pair_context_reads_the_whole_selector_WORD_and_preserves_unwritten_bytes(ushort selector, uint expectedFlags)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(14, selector).WriteWord(32, 0xF123);
        NativeBattleEffectArena context = PairContext633090(NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(16, BitConverter.GetBytes(0x12345678u)), task);
        Assert.Equal(0xE21CC0u, Dword(context, 0));
        Assert.Equal(0u, Dword(context, 8));
        Assert.Equal(unchecked((uint)(int)(short)0xF123), Dword(context, 12));
        Assert.Equal(expectedFlags, Dword(context, 28));
        Assert.Equal(0x12345678u, Dword(context, 16));
        Assert.Throws<NotSupportedException>(() => context.Read(4, 4));
        Assert.Throws<NotSupportedException>(() => context.Read(32, 4));
    }

    [Fact]
    public void Pair_matrix_uses_signed_current_focal_WORD_and_does_not_invent_rotation_padding()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(24, 0).WriteWord(28, 4096);
        NativeBattlePackedMatrix32 packed = PairMatrix633090(task, -32760);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0)) with { X = 0, Y = 0, Z = -32760 };
        NativeBattleActionMatrix actual = packed.ReadMatrix();
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((expected.X, expected.Y, expected.Z, expected.Scale), (actual.X, actual.Y, actual.Z, actual.Scale));
        Assert.False(packed.Known[18]);
        Assert.False(packed.Known[19]);
        Assert.All(packed.Known.Take(18), value => Assert.True(value));
        Assert.All(packed.Known.Skip(20), value => Assert.True(value));
    }

    [Fact]
    public void Pair_draw_precedes_pause_and_terminal_age_with_WORD_wrapping()
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        (pool, int slot) = pool.Append(0x633090);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 60).WriteWord(24, 0x8002).WriteWord(26, unchecked((ushort)-8))) };
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [pool] };
        NativeBattleEffect218Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect218State.FromColdProcess());
        uint address = pool.BankAddress + (uint)(slot * pool.Stride);
        List<ushort> ages = [];
        NativeBattleEffect218Frame Draw(NativeBattleEffect218Frame current, uint selected)
        {
            ages.Add(Read(current, selected).ReadWord(12));
            return current;
        }
        var paused = AdvancePair633090(frame, address, 1, Draw);
        Assert.Equal(0, paused.Result);
        Assert.Equal((ushort)0x8002, Read(paused.Frame, address).ReadWord(24));
        var finished = AdvancePair633090(paused.Frame, address, 0, Draw);
        Assert.Equal(new ushort[] { 60, 60 }, ages);
        Assert.Equal(2, finished.Result);
        Assert.Equal((ushort)61, Read(finished.Frame, address).ReadWord(12));
        Assert.Equal((ushort)0x7FFA, Read(finished.Frame, address).ReadWord(24));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x633090, new byte[24].ToImmutableArray());
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
