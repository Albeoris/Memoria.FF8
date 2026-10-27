using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47ActPhysicalTests
{
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Fact]
    public void Preparation_preserves_composition_padding_unwritten_context_bytes_and_literal_field_widths()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = NativeBattleCurse47Controller.Read(fixture.Frame, fixture.Address).WriteWord(0x62, 1024).WriteWord(0x60, 1024).WriteDword(0x50, 4096).WriteDword(0x54, 2048).WriteDword(0x58, 4096).WriteWord(0x1C, 123).WriteWord(0x1E, unchecked((ushort)-456)).WriteWord(0x20, 789).WriteDword(0x70, 0x12345678).WriteDword(0x7C, 0xABCDEF01);
        foreach (int offset in new[] { 0x8C, 0x8E, 0x90, 0x92, 0x86, 0x80, 0x88 })
        {
            task = task.WriteWord(offset, (ushort)(offset * 3));
        }
        NativeBattleCurse47Frame frame = NativeBattleCurse47Controller.Write(fixture.Frame, fixture.Address, task);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 92).ToArray());
        NativeBattleEffectArena internal32 = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(18, [0xBC]);
        string stagingBefore = SnapshotJson(frame.Staging);
        NativeBattleCurse47ActPreparation prepared = NativeBattleCurse47ActRendering.PrepareDraw(frame, fixture.Address, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), retained, internal32);
        Assert.Equal(new short[] { 0, 2048, 0, 0, 0, -4096, -4096, 0, 0 }, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(prepared.Context92.Read(index * 2, 2))));
        Assert.Equal(123u, Dword(prepared.Context92, 20));
        Assert.Equal(unchecked((uint)-456), Dword(prepared.Context92, 24));
        Assert.Equal(789u, Dword(prepared.Context92, 28));
        Assert.Equal((byte)0xBC, prepared.Context92.Read(18, 1)[0]);
        Assert.False(prepared.Context92.ReadSnapshot(19, 1).Known[0]);
        Assert.All(prepared.Context92.Read(32, 24), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(0x12345678u, Dword(prepared.Context92, 0x38));
        Assert.Equal(0xABCDEF01u, Dword(prepared.Context92, 0x44));
        Assert.Equal(NativeBattleCurse47ActRendering.MorphAddress, Dword(prepared.Context92, 0x48));
        foreach ((int destination, int source) in new[] { (0x4C, 0x8C), (0x4E, 0x8E), (0x50, 0x90), (0x52, 0x92), (0x54, 0x86), (0x56, 0x80), (0x58, 0x88) })
        {
            Assert.Equal(task.ReadWord(source), BinaryPrimitives.ReadUInt16LittleEndian(prepared.Context92.Read(destination, 2)));
        }
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, prepared.Context92.Read(0x5A, 2));
        Assert.Equal(fixture.Frame.Memory.GeometryRegisters!.Translation.ToArray(), prepared.Frame.Memory.GeometryRegisters!.Translation.ToArray());
        Assert.NotEqual(stagingBefore, SnapshotJson(prepared.Frame.Staging));
    }

    [Fact]
    public void Zero_angles_do_not_call_stateful_rotation_and_unknown_internal_padding_overwrites_known_caller_padding()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = NativeBattleCurse47Controller.Read(fixture.Frame, fixture.Address).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
        NativeBattleCurse47Frame frame = NativeBattleCurse47Controller.Write(fixture.Frame with { Staging = null }, fixture.Address, task);
        NativeBattleCurse47ActPreparation prepared = NativeBattleCurse47ActRendering.PrepareDraw(frame, fixture.Address, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), NativeBattleEffectArena.Clear().Write(18, [1, 2]), NativeBattleEffectArena.Clear().MarkUnknown(0, 32));
        Assert.Null(prepared.Frame.Staging);
        Assert.Same(frame.Memory, prepared.Frame.Memory);
        Assert.All(prepared.Context92.ReadSnapshot(18, 2).Known, Assert.False);
        Assert.Equal(0x264E778u, Dword(prepared.Context92, 0x48));
    }

    [Fact]
    public void Emission_publishes_bounded_temporary_and_current_GTE_before_COMMON_delivery_and_retains_private_cursors()
    {
        var fixture = Fixture();
        NativeBattleCurse47Frame frame = fixture.Frame with { Staging = fixture.Frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 8, new byte[] { 1, 2, 3, 4 }) };
        int delivered = 0;
        NativeBattleCurse47Frame result = NativeBattleCurse47ActRendering.EmitNode(frame, Source(), Context(), Node(0), new(null, 100, 160, 108), Constants, (published, batch) =>
        {
            delivered++;
            Assert.NotEmpty(batch.Attempts);
            NativeBattleEffectArena physical = NativeBattleTemporaryMemory.ReadView(published.Staging!, NativeBattleTemporaryMemory.Address, 104);
            Assert.Equal(0x2030u, Dword(physical, 0x14));
            Assert.Equal(0x04030201u, Dword(physical, 8));
            Assert.Equal(0x17BB150u, Dword(physical, 4));
            Assert.NotNull(published.Memory.ScriptedColorRegisters!.Mac[0]);
            Assert.Equal(0x20E3AB8u, published.State.ReadDword(0x264B3CC));
            Assert.Equal(0x20E7AB8u, published.State.ReadDword(0x264B4D4));
            Assert.Equal(NativeBattleTemporaryMemory.Address + 104, BinaryPrimitives.ReadUInt32LittleEndian(published.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            return published with { Memory = published.Memory with { SoundPublishedFlags = 0x77 }, State = published.State.WithSharedWord(0x10, 0x55) };
        });
        Assert.Equal(1, delivered);
        Assert.Equal(0x77u, result.Memory.SoundPublishedFlags);
        Assert.Equal((ushort)0x55, result.State.SharedWord(0x10));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Equal(0x20E3AB8u, result.State.ReadDword(0x264B3CC));
        Assert.Equal(0x20E7AB8u, result.State.ReadDword(0x264B4D4));
    }

    [Fact]
    public void Morph_unknown_W_and_physical_temporary_survive_cold_next_with_current_common_delivery()
    {
        var fixture = Fixture();
        NativeBattleCurse47Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteView(NativeBattleCurse47ActRendering.MorphAddress, NativeBattleEffectArena.Clear().MarkUnknown(0, 840), 840) };
        NativeBattleCurse47Frame result = Draw(frame);
        Assert.All(result.State.ReadView(NativeBattleCurse47ActRendering.MorphAddress, 24).ReadSnapshot(6, 2).Known, Assert.False);
        NativeBattleCurse47Frame cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleCurse47Frame>(SnapshotJson(result))!);
        Assert.Equal(SnapshotJson(Draw(result)), SnapshotJson(Draw(cold)));
        NativeBattleCurse47Frame Draw(NativeBattleCurse47Frame current) => NativeBattleCurse47ActRendering.EmitNode(current, Source(), Context(), Node(2048), new(null, 100, 160, 108), Constants, (published, batch) =>
        {
            Assert.NotEmpty(batch.Attempts);
            NativeBattleEffectArena physical = NativeBattleTemporaryMemory.ReadView(published.Staging!, NativeBattleTemporaryMemory.Address, 104);
            Assert.Equal(NativeBattleCurse47ActRendering.MorphAddress, Dword(physical, 4));
            return published;
        });
    }

    [Fact]
    public void Invisible_nodes_do_not_allocate_or_require_missing_physical_or_GTE_owners()
    {
        var fixture = Fixture();
        byte[] bytes = Node(0).Data.ToArray();
        Array.Clear(bytes, 24, 6);
        NativeBattleCurse47Frame frame = fixture.Frame with { Staging = null, Memory = fixture.Frame.Memory with { GeometryRegisters = null, ScriptedColorRegisters = null } };
        NativeBattleCurse47Frame result = NativeBattleCurse47ActRendering.EmitNode(frame, Source(), Context(), new(0, 0, bytes.ToImmutableArray()), new(null, 100, 160, 108), Constants, (_, _) => throw new InvalidOperationException("Invisible callback must not deliver."));
        Assert.Same(frame, result);
    }

    private static NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().Write(0, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null).Bytes.AsSpan()).Write(0x58, [0, 0]);
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleGfAnimationNode Node(short ratio)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x200);
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
        return new(0x17BB134, bytes.ToImmutableArray());
    }
    private static (NativeBattleCurse47Frame Frame, uint Address) Fixture()
    {
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(4, NativeBattleCurse47Act.FirstCallback, 648);
        NativeBattleOriginalEffectState effect = new(47, 47, new(47, NativeBattleCurse47Controller.Entry, 0x82A7D0), null, null) { Pools = appended.Hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Translation = [1, 2, 3] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAA123456, Rgb = [1, 2, 3] } };
        NativeBattleCurse47State state = NativeBattleCurse47IoTests.Initialized().WriteDword(0x264B3CC, 0x20E3AB8).WriteDword(0x264B4D4, 0x20E7AB8);
        return (new NativeBattleCurse47Frame(memory, effect, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) }, appended.Address);
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
