using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ActRenderingTests
{
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Fact]
    public void Preparation_rotates_Y_then_X_and_copies_actual_composition_padding_and_header_fields()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(fixture.Frame, fixture.Address).WriteWord(0x62, 1024).WriteWord(0x60, 1024).WriteDword(0x50, 4096).WriteDword(0x54, 2048).WriteDword(0x58, 4096).WriteWord(0x1C, 123).WriteWord(0x1E, unchecked((ushort)-456)).WriteWord(0x20, 789).WriteDword(0x70, 0x12345678).WriteDword(0x7C, 0xABCDEF01);
        foreach (int offset in new[] { 0x8C, 0x8E, 0x90, 0x92, 0x86, 0x80, 0x88 })
        {
            task = task.WriteWord(offset, (ushort)(offset * 3));
        }
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, task);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 92).ToArray());
        NativeBattleEffectArena composedTemporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(18, [0xBC]);
        NativeBattleDoom8ActPreparation prepared = NativeBattleDoom8ActRendering.Prepare8C59E0(frame, fixture.Address, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), context, composedTemporary);
        Assert.Equal(new short[] { 0, 2048, 0, 0, 0, -4096, -4096, 0, 0 }, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(prepared.Context92.Read(index * 2, 2))));
        Assert.Equal(123u, Dword(prepared.Context92, 20));
        Assert.Equal(unchecked((uint)-456), Dword(prepared.Context92, 24));
        Assert.Equal(789u, Dword(prepared.Context92, 28));
        Assert.Equal((byte)0xBC, prepared.Context92.Read(18, 1)[0]);
        Assert.False(prepared.Context92.ReadSnapshot(19, 1).Known[0]);
        Assert.All(prepared.Context92.Read(32, 24), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(0x12345678u, Dword(prepared.Context92, 0x38));
        Assert.Equal(0xABCDEF01u, Dword(prepared.Context92, 0x44));
        Assert.Equal(NativeBattleDoom8ActRendering.MorphAddress, Dword(prepared.Context92, 0x48));
        Assert.Equal((ushort)(0x88 * 3), BinaryPrimitives.ReadUInt16LittleEndian(prepared.Context92.Read(0x58, 2)));
        Assert.Equal(JsonSerializer.Serialize(frame.Random), JsonSerializer.Serialize(prepared.Frame.Random));
        Assert.Equal(frame.Memory.GeometryRegisters!.Translation.ToArray(), prepared.Frame.Memory.GeometryRegisters!.Translation.ToArray());
    }

    [Theory]
    [InlineData(0x200u, 0x2030u)]
    [InlineData(0x600u, 0x2030u)]
    [InlineData(0x2200u, 0x2030u)]
    [InlineData(0x4200u, 0x2000u)]
    [InlineData(0x6600u, 0x2000u)]
    public void Node_uses_actual_Euler_selector_and_ignores_unread_header_UV_flag(uint header, uint flags)
    {
        var fixture = Fixture();
        NativeBattleGfAnimationNode node = Node(header, 0);
        NativeBattleEffectArena context = Context();
        int delivered = 0;
        NativeBattleDoom8Frame result = NativeBattleDoom8ActRendering.DrawNode8C5B10(fixture.Frame, Source(), context, node, new(null, 100, 160, 108), Constants, Deliver);
        Assert.Equal(1, delivered);
        NativeBattleActionMatrix expected = (header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        Assert.Equal(expected.Rotation.ToArray(), Enumerable.Range(0, 9).Select(index => unchecked((short)(result.Memory.GeometryRegisters!.Rotation20[index * 2]!.Value | result.Memory.GeometryRegisters.Rotation20[index * 2 + 1]!.Value << 8))));
        Assert.Equal(fixture.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));

        NativeBattleDoom8Frame Deliver(NativeBattleDoom8Frame frame, NativeBattleGfGeometryBatch batch)
        {
            delivered++;
            NativeBattleEffectArena physical = NativeBattleTemporaryMemory.ReadView(frame.Staging!, NativeBattleTemporaryMemory.Address, 104);
            Assert.Equal(flags, Dword(physical, 0x14));
            return frame;
        }
    }

    [Fact]
    public void Morph_retains_unknown_W_and_publishes_current_registers_before_delivery_and_cold_next()
    {
        var fixture = Fixture();
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteView(NativeBattleDoom8ActRendering.MorphAddress, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleDoom8ActRendering.MorphLength), NativeBattleDoom8ActRendering.MorphLength) };
        NativeBattleDoom8Frame result = Draw(frame);
        Assert.All(result.State.ReadView(NativeBattleDoom8ActRendering.MorphAddress, 24).ReadSnapshot(6, 2).Known, Assert.False);
        Assert.Equal(0x77u, result.Memory.SoundPublishedFlags);
        NativeBattleDoom8Frame cold = JsonSerializer.Deserialize<NativeBattleDoom8Frame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(Draw(result)), JsonSerializer.Serialize(Draw(cold)));

        NativeBattleDoom8Frame Draw(NativeBattleDoom8Frame current) => NativeBattleDoom8ActRendering.DrawNode8C5B10(current, Source(), Context(), Node(0x200, 2048), new(null, 100, 160, 108), Constants, (published, batch) =>
        {
            Assert.NotEmpty(batch.Attempts);
            NativeBattleEffectArena physical = NativeBattleTemporaryMemory.ReadView(published.Staging!, NativeBattleTemporaryMemory.Address, 104);
            Assert.Equal(NativeBattleDoom8ActRendering.MorphAddress, Dword(physical, 4));
            Assert.NotNull(published.Memory.ScriptedColorRegisters!.Mac[0]);
            return published with { Memory = published.Memory with { SoundPublishedFlags = 0x77 } };
        });
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)2048)]
    public void Borrowed_staging_ACT_matches_catalog_and_preserves_the_active_source_lifetime_on_failure(short ratio)
    {
        NativeBattleGfAnimationSource catalog = Source();
        var fixture = Fixture();
        NativeBattleFileStaging staging = fixture.Frame.Staging!.Write(catalog.Address, catalog.Data.AsSpan());
        NativeBattleDoom8Frame original = fixture.Frame with { Staging = staging };
        NativeBattleDoom8Frame expected = Draw(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(original)), catalog);
        NativeBattleGfAnimationSource borrowed = NativeBattleGfAnimationSource.Borrow(catalog.Address, staging, catalog.Length);
        Assert.Throws<InvalidOperationException>(() => borrowed.Data);
        NativeBattleDoom8Frame actual = Draw(original, borrowed);
        Assert.Equal(SnapshotJson(expected), SnapshotJson(actual));
        Assert.Null(borrowed.ActiveReader);

        using (NativeBattleGfSourceReader retained = borrowed.OpenReader())
        {
            staging.WriteWithKnownness(catalog.Address + 8, catalog.Data.AsSpan().Slice(8, 4), new bool[4]);
            // A nested draw consumes the original selected source even after an overlapping callback store.
            NativeBattleDoom8Frame preserved = Draw(actual, borrowed);
            Assert.NotNull(preserved.Memory.GeometryRegisters);
            Assert.Same(retained, borrowed.ActiveReader);
        }
        Assert.Null(borrowed.ActiveReader);
        Assert.Throws<NotSupportedException>(() => Draw(actual, borrowed));
        Assert.Null(borrowed.ActiveReader);
        staging.Write(catalog.Address + 8, catalog.Data.AsSpan().Slice(8, 4));
        Assert.NotNull(Draw(actual, borrowed).Memory.GeometryRegisters);
        Assert.Null(borrowed.ActiveReader);

        NativeBattleDoom8Frame Draw(NativeBattleDoom8Frame frame, NativeBattleGfAnimationSource source) => NativeBattleDoom8ActRendering.DrawNode8C5B10(frame, source, Context(), Node(0x200, ratio), new(null, 100, 160, 108), Constants, (current, _) => current);
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));

    private static NativeBattleEffectArena Context()
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        return NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan()).Write(0x58, [0, 0]);
    }

    private static NativeBattleGfAnimationNode Node(uint header, short ratio)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 128);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 256);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 512);
        foreach (int offset in new[] { 24, 26, 28 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[128];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 60);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 3);
        short[] positions = [-400, -400, 400, -400, -400, 400];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 3; vertex++)
            {
                int offset = 28 + shape * 24 + vertex * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)(positions[vertex * 2] + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), (short)(positions[vertex * 2 + 1] + shape * 200));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 6), 0x1234);
            }
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(84), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(88), 0x24332211);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(94), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(96), 4);
        return new(0x2100000, bytes.ToImmutableArray());
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static (NativeBattleDoom8Frame Frame, uint Address) Fixture()
    {
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(4, 0x8C58B0, 1112);
        NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = appended.Hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Translation = [1, 2, 3] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAA123456, Rgb = [1, 2, 3] } };
        return (new NativeBattleDoom8Frame(memory, effect, new(1), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray())) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) }, appended.Address);
    }
}
