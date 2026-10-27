using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79LowerTests
{
    [Theory]
    [InlineData(7, 52)]
    [InlineData(3, 40)]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    public void Selected_groups_preserve_zero_area_and_depth_bias_before_shift(int group, int length)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleEffect79Mesh104Step result = NativeBattleEffect79Mesh104.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(mesh.Address), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(length, result.Batch.AllocationBytes);
        Assert.Equal(group == 4 ? 205 : 206, primitive.Bucket);
        Assert.Equal(group == 4 ? 755u : 756u, U(result.Context104, 0x38));
        Assert.Equal(0u, U(result.Context104, 0x30));
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context104, 0x2C));
        Assert.Equal(mesh.Address + 8, U(result.Context104, 4));
        Assert.Equal(0xDEADBEEFu, U(result.Context104, 0x40));
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result));
        NativeBattleEffect79Mesh104Step restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect79Mesh104Step>(json)!);
        Assert.Equal(result.Batch.AllocationBytes, restored.Batch.AllocationBytes);
        Assert.Equal(result.Memory.GeometryRegisters!.Sxy.ToArray(), restored.Memory.GeometryRegisters!.Sxy.ToArray());
    }

    [Theory]
    [InlineData(-86)]
    [InlineData(-43)]
    [InlineData(945)]
    [InlineData(2324)]
    public void Morph_uses_current_signed_shape_alias_and_executes_zero_factor_arithmetic(int aliasIndex)
    {
        const uint basis = 0x1762E40;
        byte[] bank = new byte[31512];
        uint mesh = basis + 0x200;
        uint a = basis + 0x400;
        uint b = basis + 0x800;
        uint channel = basis + 0x1000;
        BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(0x204), 2);
        foreach (int offset in new[] { 0x400, 0x800 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(offset + 0x1CA), (short)aliasIndex);
        }
        for (int vertex = 0; vertex < 2; vertex++)
        {
            int offset = vertex * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x408 + offset), (short)(100 + vertex));
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x40A + offset), -20);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x40C + offset), 30);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x808 + offset), 900);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x208 + offset + 6), 119);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x1000), 4096);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleEffect79Frame frame = new(Memory(), new(79, 78, new(79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79Profile.Initializer), null, null), new(1), NativeBattleEffect79State.FromColdProcess()) { Staging = staging };
        NativeBattleEffect79MorphStep result = NativeBattleEffect79Morph.Execute7BA230(frame, basis, bank.ToImmutableArray(), mesh, a, b, channel);
        Assert.Equal(new short[] { 100, -20, 30, 119, 101, -20, 30, 119 }, Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Bank.AsSpan().Slice(0x208 + index * 2))));
        Assert.Equal(0u, result.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 101, -20, 30 }, result.Frame.Memory.ScriptedColorRegisters.Ir.ToArray());
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(4096, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 4, 4)));
        Assert.Null(frame.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Fact]
    public void Null_shape_skips_the_native_temporary_and_register_consumers()
    {
        NativeBattleEffect79Frame frame = new(Memory(), new(79, 78, new(79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79Profile.Initializer), null, null), new(1), NativeBattleEffect79State.FromColdProcess());
        ImmutableArray<byte> bank = new byte[31512].ToImmutableArray();
        NativeBattleEffect79MorphStep result = NativeBattleEffect79Morph.Execute7BA230(frame, 0x1762E40, bank, 0, 0, 1, uint.MaxValue);
        Assert.Same(frame, result.Frame);
        Assert.Equal(bank, result.Bank);
    }

    [Fact]
    public void Selected_morph_hook_bypasses_the_generic_geometry_clock_ratio_read()
    {
        const uint basis = 0x10000000;
        byte[] bytes = new byte[2048];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, basis + 256);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), basis + 512);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), basis + 1024);
        bytes[32] = 1;
        byte[] source = new byte[344];
        source[0x16] = 5;
        source[0x1B] = 255;
        for (int field = 0xC8; field <= 0x144; field += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(field), basis + 128);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0x130), basis + 33);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0x134), basis + 32);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0x138), uint.MaxValue);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 676, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteDword(0x34 + 0x22C, basis).WriteDword(0x34 + 0x230, basis + 16);
        pool.WriteSlot(0, task);
        NativeBattleOriginalEffectState effect = new(79, 78, new(79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79Profile.Initializer), null, null) { Pools = [pool] };
        NativeBattleEffectNodeMemory memory = new(Memory(), effect, 0x200, 0, 0x10E0, 0x10E0, 0x10680);
        NativeBattleEffectNodeBank bank = new(basis, bytes.ToImmutableArray());
        int calls = 0;
        NativeBattleEffectNodeInputs inputs = new(NativeBattleCuragaMatrices.Identity8DD770(), _ => throw new InvalidOperationException(), (_, _, _) => null, (_, _) => null)
        {
            SelectedMorph = (mesh, first, second, channel) =>
            {
                calls++;
                Assert.Equal(basis + 256, mesh);
                Assert.Equal(basis + 512, first);
                Assert.Equal(basis + 1024, second);
                Assert.Equal(uint.MaxValue, channel);
            }
        };
        new NativeBattleEffectNodeLifecycle(memory, bank, inputs).AdvanceDrawable(0x10E0, source);
        Assert.Equal(1, calls);
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectNodeLifecycle(memory, bank, inputs with { SelectedMorph = null }).AdvanceDrawable(0x10E0, source));
    }

    [Fact]
    public void Act_selected_vertices_use_current_rows_and_preserve_unknown_w()
    {
        NativeBattleStaticMeshResource mesh = Mesh(3);
        byte[] selectedMesh = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(selectedMesh.AsSpan(4), 125);
        BinaryPrimitives.WriteUInt16LittleEndian(selectedMesh.AsSpan(40 + 16 + 10), 248);
        mesh = mesh with { Bytes = selectedMesh.ToImmutableArray() };
        NativeBattleEffectArena context = Context(mesh.Address).Write(4, BitConverter.GetBytes(0x25EAFA8u)).Write(0x14, BitConverter.GetBytes(0x2030u)).Write(0x10, BitConverter.GetBytes(-512));
        List<int> selected = [];
        NativeBattleEffect79Mesh104Step result = NativeBattleEffect79Mesh104.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, offset =>
        {
            selected.Add(offset);
            return (new NativeBattleVector(0, 0, 0), null);
        });
        Assert.Equal(new[] { 0, 8, 16, 992 }, selected);
        Assert.Equal(0x25EAFA8u, U(result.Context104, 4));
        Assert.Equal(0u, U(result.Context104, 0x38));
        Assert.Equal(17, Assert.Single(result.Batch.Attempts).Primitive!.Bucket);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Mesh104.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void Actor_anchor_hook_runs_only_for_selector_two_before_rotated_source_offset(int selector, int expectedCalls)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 676, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteWord(0x34 + 0x1E, 4).WriteDword(0x34 + 0x1D4, 10u << 16).WriteDword(0x34 + 0x1E4, 20u << 16).WriteDword(0x34 + 0x1F4, 30u << 16);
        pool.WriteSlot(0, task);
        NativeBattleOriginalEffectState effect = new(79, 78, new(79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79Profile.Initializer), null, null) { Pools = [pool] };
        NativeBattleEffectNodeMemory memory = new(Memory(), effect, 0x200, 0, 0x870, 0x870, 0x9D80);
        int calls = 0;
        NativeBattleEffectNodeInputs inputs = new(NativeBattleCuragaMatrices.Identity8DD770(), _ => throw new InvalidOperationException(), (_, _, _) => null, (_, _) => null)
        {
            ActorAnchor = (int actor, ref NativeCrtRandomState current) =>
            {
                Assert.Equal(4, actor);
                Assert.Equal(0u, memory.Dword(0x4C));
                current = current.Draw(out ushort value);
                Assert.InRange(value % 18, 0, 17);
                calls++;
                return new(50, 60, 70);
            }
        };
        byte[] source = new byte[344];
        source[0x11] = 1;
        source[0x25] = (byte)selector;
        BinaryPrimitives.WriteInt32LittleEndian(source, 3 << 16);
        NativeCrtRandomState initial = new(1234);
        NativeCrtRandomState random = initial;
        new NativeBattleEffectNodeLifecycle(memory, new(0x10000000, new byte[344].ToImmutableArray()), inputs).InitializeEmitter(0, source, ref random);
        Assert.Equal(expectedCalls, calls);
        Assert.Equal((uint)((selector == 2 ? 53 : selector == 1 ? 23 : 13) << 16), memory.Dword(0x4C));
        Assert.Equal(expectedCalls == 0 ? initial : initial.Draw(out _), random);
    }

    [Fact]
    public void Programme_uses_completed_staging_and_exact_clear_extent_then_serializes_next_workspace()
    {
        const uint sourceAddress = 0x19F1018;
        byte[] original = new byte[5292];
        BinaryPrimitives.WriteInt32LittleEndian(original.AsSpan(4), 16);
        byte[] current = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(current.AsSpan(20), 1);
        NativeBattleEffect79Resources resources = new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleEffect79NodeResourceBank>.Empty, null!, ImmutableDictionary<short, NativeBattleEffectFileAsset>.Empty.Add(396, new(396, "fixture", original.ToImmutableArray())), ImmutableDictionary<uint, uint>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, [], 0);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 704, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteBytes(12, Enumerable.Repeat((byte)0xAA, 692).ToArray()).WriteDword(0x74, sourceAddress).WriteDword(0x78, 236);
        pool.WriteSlot(0, task);
        NativeBattleOriginalEffectState effect = new(79, 78, new(79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79Profile.Initializer), null, null) { Pools = [pool] };
        NativeBattleEffect79Frame frame = new(Memory(), effect, new(1), NativeBattleEffect79State.FromColdProcess()) { Staging = NativeBattleFileStaging.Unknown().Write(sourceAddress, current) };
        NativeBattleEffect79Frame initialized = NativeBattleEffect79Programme.Initialize7016B0(frame, 0x200, sourceAddress, 236, resources);
        NativeBattleModulePoolTask written = NativeBattleEffect79Controller.Read(initialized, 0x200);
        Assert.Equal(sourceAddress, written.ReadDword(0x94));
        Assert.Equal(0u, written.ReadDword(0x98));
        Assert.Equal(0u, written.ReadDword(0x94 + 232));
        Assert.Equal(0xAAAAAAAAu, written.ReadDword(0x94 + 236));
        NativeBattleEffect79ProgrammeStep advanced = NativeBattleEffect79Programme.Advance701970(initialized, 0x200, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), resources, null!);
        Assert.Equal(1u, NativeBattleEffect79Controller.Read(advanced.Frame, 0x200).ReadDword(0x98));
        Assert.Equal(0, advanced.Remaining);
        NativeBattleEffect79Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect79Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(advanced.Frame)))!);
        NativeBattleEffect79ProgrammeStep next = NativeBattleEffect79Programme.Advance701970(restored, 0x200, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), resources, null!);
        Assert.Equal(1u, NativeBattleEffect79Controller.Read(next.Frame, 0x200).ReadDword(0x98));
        Assert.Equal(0, next.Remaining);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Programme.Initialize7016B0(frame with { Staging = NativeBattleFileStaging.Unknown() }, 0x200, sourceAddress, 236, resources));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Context(uint address) => NativeBattleEffectArena.Clear().MarkUnknown(0, 104).Write(0, BitConverter.GetBytes(address)).Write(0x10, BitConverter.GetBytes(256)).Write(0x14, BitConverter.GetBytes(0x30u)).Write(0x18, new byte[4]).Write(0x40, BitConverter.GetBytes(0xDEADBEEFu));
    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = new byte[40 + 32 + lengths[group]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        int cursor = 40;
        for (int index = 0; index < 8; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), index == group ? 1 : 0);
            cursor += 4;
            if (index == group)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), (uint)(group == 3 ? 0x2C : group == 4 ? 0x30 : group == 5 ? 0x38 : 0x3C) << 24 | 0x112233);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 6), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 8), 4);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 6);
                cursor += lengths[group];
            }
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
