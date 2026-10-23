using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSigh46Controller;
using static Memoria.FF8.Runtime.NativeBattleSigh46Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSigh46LowerTests
{
    [Fact]
    public void Custom_initialization_uses_current_model_and_ordinary_copy_order_then_hides_without_drawing()
    {
        var fixture = Fixture(3, 0x8359C0, 0);
        var task = Read(fixture.Frame, fixture.Address).MarkPayloadUnknown(0x66, 2);
        var frame = Write(fixture.Frame, fixture.Address, task);
        List<NativeBattleSigh46Operation> operations = [];
        var result = AdvanceCustom8359C0(frame, fixture.Address, fixture.Resources, Inputs(operations));
        Assert.Equal(0, result.Result);
        task = Read(result.Frame, fixture.Address);
        Assert.Equal((short)100, Signed(task, 0x1C));
        Assert.Equal((short)200, Signed(task, 0x1E));
        Assert.Equal((short)-468, Signed(task, 0x20));
        Assert.Equal((ushort)1, task.ReadWord(0x24));
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((byte)4, (byte)(task.ReadByte(0x26) & 4));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x66));
        Assert.Equal(new NativeBattleEffectVramCopy(512, 256, 64, 256, 704, 256), Assert.IsType<NativeBattleSigh46VramCopy505EB0>(operations[0]).Copy);
        Assert.Equal(new NativeBattleEffectVramCopy(672, 256, 64, 256, 768, 256), Assert.IsType<NativeBattleSigh46VramCopy505EB0>(operations[1]).Copy);
        Assert.Equal(2, operations.Count);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Custom_old_age_four_spawns_and_draws_six_meshes_in_the_same_callback(int age, bool visible)
    {
        var fixture = Fixture(3, 0x8359C0, 1);
        var task = Read(fixture.Frame, fixture.Address).WriteWord(0x24, (ushort)age).WriteByte(0x26, 4).MarkPayloadUnknown(0x66, 2);
        var frame = Write(fixture.Frame, fixture.Address, task);
        List<NativeBattleSigh46Operation> operations = [];
        var result = AdvanceCustom8359C0(frame, fixture.Address, fixture.Resources, Inputs(operations));
        Assert.Equal((ushort)(age + 1), Read(result.Frame, fixture.Address).ReadWord(0x24));
        Assert.Equal(visible ? 10 : 0, operations.Count);
        if (visible)
        {
            Assert.Equal(new[] { MeshAAddress, MeshBAddress, MeshAAddress, MeshBAddress, MeshAAddress, MeshBAddress }, operations.OfType<NativeBattleSigh46DrawMesh88>().Select(draw => draw.SourceAddress));
            var child = Assert.Single(result.Frame.Effect.Pools[4].Slots, value => (value.Flags & 1) != 0);
            Assert.Equal(0x835E90u, child.Callback);
            Assert.Throws<NotSupportedException>(() => child.ReadWord(0x6A));
            Assert.Equal((byte)1, Read(result.Frame, fixture.Address).ReadByte(0x28));
            Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(result.Frame));
        }
    }

    [Theory]
    [InlineData(22, 2)]
    [InlineData(23, 3)]
    public void Custom_wait_uses_signed_old_age_greater_than_twenty_two(int age, int phase)
    {
        var fixture = Fixture(3, 0x8359C0, 2);
        var frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x24, (ushort)age));
        var result = AdvanceCustom8359C0(frame, fixture.Address, fixture.Resources, Inputs([]));
        Assert.Equal((byte)phase, Read(result.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)(age + 1), Read(result.Frame, fixture.Address).ReadWord(0x24));
    }

    [Theory]
    [InlineData(0, 1024, 0)]
    [InlineData(14, 4096, 2)]
    public void Fade_increments_table_index_before_read_and_draws_its_final_full_cue(int oldIndex, int cue, int resultCode)
    {
        var fixture = Fixture(3, 0x8359C0, 3);
        var frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x88, (ushort)oldIndex));
        List<NativeBattleSigh46Operation> operations = [];
        var input = Inputs(operations) with { Execute = (current, operation) =>
        {
            operations.Add(operation);
            if (operation is NativeBattleSigh46DrawMesh88 mesh)
            {
                Assert.Equal((uint)cue, Dword(Context(current, mesh.TemporaryAddress, 88), 12));
                Assert.Equal(NativeBattleTemporaryMemory.Address, mesh.TemporaryAddress);
                Assert.Equal(0xF0u, Dword(Context(current, mesh.TemporaryAddress, 88), 0x1C));
                Assert.Equal(0x2793E58u, current.State.ReadDword(0x26527A0));
            }
            return current;
        } };
        var actual = AdvanceCustom8359C0(frame, fixture.Address, fixture.Resources, input);
        Assert.Equal(resultCode, actual.Result);
        Assert.Equal((ushort)(oldIndex + 1), Read(actual.Frame, fixture.Address).ReadWord(0x88));
        Assert.Equal(6, operations.OfType<NativeBattleSigh46DrawMesh88>().Count());
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Fact]
    public void Visible_pass_zero_extends_wrapped_phase_bytes_and_saturates_only_material_selector()
    {
        var fixture = Fixture(3, 0x8359C0, 2);
        var frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x84, 0xAAF8).WriteWord(0x86, 0xBBFA).WriteWord(0x82, 16));
        List<NativeBattleSigh46Operation> operations = [];
        var actual = AdvanceCustom8359C0(frame, fixture.Address, fixture.Resources, Inputs(operations));
        Assert.Equal((ushort)0, Read(actual.Frame, fixture.Address).ReadWord(0x84));
        Assert.Equal((ushort)0, Read(actual.Frame, fixture.Address).ReadWord(0x86));
        Assert.Equal((ushort)16, Read(actual.Frame, fixture.Address).ReadWord(0x82));
        Assert.Equal(new NativeBattleEffectVramCopy(704, 256, 64, 256, 512, 256), Assert.IsType<NativeBattleSigh46VramCopy505EB0>(operations[0]).Copy);
        Assert.Equal(new NativeBattleEffectVramCopy(768, 256, 32, 256, 672, 256), Assert.IsType<NativeBattleSigh46VramCopy505EB0>(operations[1]).Copy);
        Assert.Equal(8, operations.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(255)]
    public void Wrapping_copy_preserves_exact_VRAM_rectangles_and_queue_order(int phase)
    {
        var fixture = Fixture(3, 0x8359C0, 4);
        List<NativeBattleSigh46Operation> operations = [];
        _ = ScrollVram46(fixture.Frame, 768, 32, 672, (ushort)phase, Inputs(operations));
        Assert.Equal(new NativeBattleEffectVramCopy(768, 256, 32, (short)(256 - phase), 672, (short)(256 + phase)), Assert.IsType<NativeBattleSigh46VramCopy505EB0>(operations[0]).Copy);
        Assert.Equal(phase == 0 ? 1 : 2, operations.Count);
        if (phase != 0)
        {
            Assert.Equal(new NativeBattleEffectVramCopy(768, (short)(512 - phase), 32, (short)phase, 672, 256), Assert.IsType<NativeBattleSigh46VramCopy505EB0>(operations[1]).Copy);
        }
    }

    [Fact]
    public void Material_row_overwrites_only_four_DWORDs_per_GT4_and_retains_unknown_vertex_W()
    {
        var fixture = Fixture(3, 0x8359C0, 4);
        var state = fixture.Frame.State.WriteView(MeshAAddress + 14, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
        var frame = fixture.Frame with { State = state };
        byte[] before = state.ReadView(MeshAAddress, MeshLength).ReadSnapshot(0, MeshLength).Data;
        byte[] colors = Enumerable.Range(0, 960).Select(index => unchecked((byte)(index * 13 + 7))).ToArray();
        var original = fixture.Resources.ColorStreams[16];
        var resources = fixture.Resources with { ColorStreams = fixture.Resources.ColorStreams.SetItem(16, original with { Bytes = colors.ToImmutableArray() }) };
        var actual = CopyMaterial46(frame, resources, MeshAAddress, ColorTableAAddress, 16);
        byte[] expected = before.ToArray();
        for (int face = 0; face < 60; face++)
        {
            int[] offsets = [0, 24, 28, 32];
            for (int color = 0; color < 4; color++)
            {
                colors.AsSpan(face * 16 + color * 4, 4).CopyTo(expected.AsSpan(656 + face * 36 + offsets[color]));
            }
        }
        Assert.Equal(expected, actual.State.ReadView(MeshAAddress, MeshLength).ReadSnapshot(0, MeshLength).Data);
        Assert.Throws<NotSupportedException>(() => actual.State.ReadWord(MeshAAddress + 14));
        Assert.Equal(fixture.Frame.State.Read(MeshBAddress, MeshLength), actual.State.Read(MeshBAddress, MeshLength));
        Assert.Equal(frame.Memory, actual.Memory);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    public void Generic_engine_has_no_mouth_bone_calls_and_uses_its_actual_pass_counters(int phase, int result)
    {
        var fixture = Fixture(2, 0x832100, 0);
        var frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x54, (ushort)phase).WriteWord(0x48, 5).WriteWord(0x4A, 7));
        var actual = AdvanceEngine832BE0(frame, fixture.Address, fixture.Resources, Inputs([]) with { ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException("No48 mouth producer is reached.") });
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)5, actual.Frame.State.ReadWord(0x265279A));
        Assert.Equal((ushort)7, actual.Frame.State.ReadWord(0x265278A));
        Assert.Equal((ushort)(phase == 0 ? 1 : phase), Read(actual.Frame, fixture.Address).ReadWord(0x54));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(180)]
    public void Node_scratch_and_ordinary_custom_temporary_allocators_remain_distinct(int size)
    {
        var fixture = Fixture(3, 0x8359C0, 4);
        var scratch = AllocateNodeScratch(fixture.Frame, size);
        Assert.Equal(0x2793E58u - (uint)size, scratch.Address);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(scratch.Frame));
        var ordinary = Allocate(scratch.Frame, 88);
        Assert.Equal(NativeBattleTemporaryMemory.Address, ordinary.Address);
        Assert.Equal(scratch.Address, ordinary.Frame.State.ReadDword(0x26527A0));
        var final = ReleaseNodeScratch(Release(ordinary.Frame, 88), size);
        Assert.Equal(0x2793E58u, final.State.ReadDword(0x26527A0));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(final));
    }

    internal static (NativeBattleSigh46Frame Frame, uint Address, NativeBattleSigh46Resources Resources) Fixture(int selectedPool, uint callback, byte phase)
    {
        var resources = NativeBattleSigh46StateTests.Resources();
        var pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        var root = pools[0];
        (root, _) = root.Append(RootCallback);
        pools = pools.SetItem(0, root);
        var selected = pools[selectedPool];
        (selected, int slot) = selected.Append(callback);
        pools = pools.SetItem(selectedPool, selected);
        uint address = selected.BankAddress + (uint)(slot * selected.Stride);
        NativeBattleOriginalEffectState effect = new(46, 45, new(46, Entry, Initializer), new(3, 1, 0x7F, 1, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleSigh46Frame frame = new(memory, effect, new(123), NativeBattleSigh46State.FromColdProcess().EnsureSources(resources).WriteDword(0x26527A0, 0x2793E58)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(0x10, RootBank).WriteDword(0x0C, 0x1D99A78).WriteWord(0x5A, 1));
        var task = Read(frame, address).WriteDword(0x10, RootBank).WriteDword(0x0C, 0x1D99A78).WriteByte(0x2C, 3).WriteByte(0x29, phase).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteDword(0x74, MeshAAddress).WriteDword(0x78, MeshBAddress);
        var matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0xBEEF);
        task = CopyTask(task, 0x54, PackedArena(matrix), 32);
        return (Write(frame, address, task), address, resources);
    }

    internal static NativeBattleSigh46Inputs Inputs(List<NativeBattleSigh46Operation> operations) => new(Packets(), NativeBattleGteColorInterpolation.CodeConstants45F270, uint.MaxValue, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, actor) => new(actor, 2) { Rotation14 = 0, Transform = new(new(0, 0, 0), new(100, 200, 300), 0, 0, 4096) }, (frame, _, _, _) => new(frame, PointView(new(1, 2, 3))), _ => 0, _ => Packets(), (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });
    private static uint Cursor(NativeBattleSigh46Frame frame) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));

    private static NativeBattleActionPacketsState Packets()
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 85, 46, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 85, 46, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(1, 1);
    }
}
