using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect345ParticlesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Appearance_keeps_camera_registers_full_mac_and_branch_specific_depth_storage(int count)
    {
        const uint source = 0x1643698;
        byte[] bytes = new byte[18932];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), source + 0x200);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x238), 5);
        (NativeBattleEffect345Frame frame, uint task) = Frame();
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 };
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(camera, null) }, State = frame.State.WriteDword(0x2557208, 0x2793E58) with { NodeData = ImmutableDictionary<uint, NativeBattleNodeSource>.Empty.Add(source, bytes.ToImmutableArray()).Add(0x164808C, new byte[9708].ToImmutableArray()) } };
        frame = NativeBattleEffect345Controller.Write(frame, task, NativeBattleEffect345Controller.Read(frame, task).WriteDword(0x34 + 0x224, source + 4));
        NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, task, 0, 0x870, 0x870, 0x9D80);
        int particle = 0x870;
        memory.Byte(particle + 0x1D8, checked((byte)count));
        for (int lane = 0; lane < 9; lane++)
        {
            memory.Word(particle + 0x8C + lane * 2, lane % 4 == 0 ? (ushort)4096 : (ushort)0);
        }
        for (int instance = 0; instance < count; instance++)
        {
            memory.Dword(particle + 0xDC + instance * 16, unchecked((uint)(-2 * 65536 - 32768)));
            memory.Dword(particle + 0xE0 + instance * 16, 3 * 65536 + 32768);
            memory.Dword(particle + 0xE4 + instance * 16, unchecked((uint)(-7 * 65536 - 32768)));
        }
        NativeBattleEffectNodeBank bank = new(source, bytes.ToImmutableArray());
        NativeBattleEffect345Frame result = NativeBattleEffect345Particles.PublishAppearance70A460(frame, memory, bank, particle);
        Assert.Equal(new int?[] { 8, 23, 23 }, result.Memory.ScriptedColorRegisters!.Mac.ToArray());
        Assert.Equal(new int?[] { 8, 23, 23 }, result.Memory.ScriptedColorRegisters.Ir.ToArray());
        Assert.Equal(new int?[] { 10, 20, 30 }, result.Memory.GeometryRegisters!.Translation.ToArray());
        Assert.Equal(unchecked((uint)(count == 1 ? 28 : 23)), memory.Dword(particle + 0xC8));
        if (count == 2)
        {
            Assert.Equal((ushort)28, memory.Word(particle + 0x1A0 + 8));
        }
        Assert.Equal(unchecked((uint)-2), memory.Dword(particle + 0xA0));
        Assert.Equal(unchecked((uint)-7), memory.Dword(particle + 0xA8));
    }

    [Fact]
    public void Relocation_occurs_once_and_includes_zero_channel_lanes()
    {
        byte[] original = new byte[18932];
        BinaryPrimitives.WriteUInt32LittleEndian(original.AsSpan(4), 0x200);
        NativeBattleEffect345State state = NativeBattleEffect345State.FromColdProcess() with { NodeData = ImmutableDictionary<uint, NativeBattleNodeSource>.Empty.Add(0x1643698, original.ToImmutableArray()).Add(0x164808C, new byte[9708].ToImmutableArray()) };
        NativeBattleEffect345Frame frame = new(Memory(), new(345, 344, new(345, NativeBattleEffect345Profile.Entry, NativeBattleEffect345Profile.Initializer), null, null), new(1), state);
        frame = NativeBattleEffect345Particles.Relocate707370(frame, 0x1643698);
        Assert.Equal(1u, frame.State.ReadDword(0x1643698));
        Assert.Equal(0x1643898u, frame.State.ReadDword(0x164369C));
        Assert.Equal(0x1643698u, frame.State.ReadDword(0x1643698 + 0x200 + 0xC8));
        frame = frame with { State = frame.State.WriteDword(0x1643698 + 0x200 + 0xC8, 0x1643798) };
        Assert.Same(frame, NativeBattleEffect345Particles.Relocate707370(frame, 0x1643698));
        Assert.Equal(0x1643798u, frame.State.ReadDword(0x1643698 + 0x200 + 0xC8));
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame.State));
        NativeBattleEffect345State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect345State>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(original));
    }

    [Fact]
    public void Phase_zero_and_terminal_phase_preserve_counts_and_wrap_the_native_aggregates()
    {
        (NativeBattleEffect345Frame frame, uint task) = Frame();
        frame = NativeBattleEffect345Controller.Write(frame, task, NativeBattleEffect345Controller.Read(frame, task).WriteWord(0x34 + 0x14, 3).WriteWord(0x34 + 0x16, 5));
        frame = frame with { State = frame.State.WriteWord(0x255EA7C, 65535).WriteWord(0x255EA40, 65534) };
        NativeBattleEffect345Step first = NativeBattleEffect345Particles.Advance7075D0(frame, task, null!, null!);
        Assert.Equal(0, first.Result);
        Assert.Equal((ushort)1, NativeBattleEffect345Controller.Read(first.Frame, task).ReadWord(0x54));
        Assert.Equal((ushort)2, first.Frame.State.ReadWord(0x255EA7C));
        Assert.Equal((ushort)3, first.Frame.State.ReadWord(0x255EA40));
        frame = NativeBattleEffect345Controller.Write(first.Frame, task, NativeBattleEffect345Controller.Read(first.Frame, task).WriteWord(0x54, 2));
        NativeBattleEffect345Step terminal = NativeBattleEffect345Particles.Advance7075D0(frame, task, null!, null!);
        Assert.Equal(1, terminal.Result);
        Assert.Equal((ushort)5, terminal.Frame.State.ReadWord(0x255EA7C));
        Assert.Equal((ushort)8, terminal.Frame.State.ReadWord(0x255EA40));
    }

    [Fact]
    public void Immediate_packet_alias_synchronization_preserves_current_bytes_and_unknownness()
    {
        (NativeBattleEffect345Frame frame, uint task) = Frame();
        NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, task, 0, 0x870, 0x870, 0x9D80);
        memory.Byte(0x69, 1);
        NativeBattleEffectNodeMemoryWrite published = memory.FlushStorage();
        NativeBattleEffectMemory packetWritten = published.Memory with { Arena = published.Memory.Arena!.Write(0x69, [0xA7]).MarkUnknown(0x6A, 1) };
        memory.SynchronizePublishedStorage(packetWritten);
        Assert.Equal((byte)0xA7, memory.Byte(0x69));
        Assert.Throws<NotSupportedException>(() => memory.Byte(0x6A));
        memory.Byte(0x6B, 9);
        NativeBattleEffectNodeMemoryWrite after = memory.FlushStorage();
        Assert.Equal((byte)0xA7, after.Memory.Arena!.Read(0x69, 1)[0]);
        Assert.Throws<NotSupportedException>(() => after.Memory.Arena!.Read(0x6A, 1));
        Assert.Equal((byte)9, after.Memory.Arena!.Read(0x6B, 1)[0]);
    }

    [Fact]
    public void Morph_uses_signed_shape_word_alias_and_retains_destination_w_and_temporary_cursor()
    {
        const uint basis = 0x1643698;
        byte[] bytes = new byte[18932];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x204), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x800 + 0x1CA), -2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0xFFC), 2048);
        short[] a = [100, 20, 30];
        short[] b = [900, 40, 50];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x408 + axis * 2), a[axis]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x808 + axis * 2), b[axis]);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x20E), 119);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Cursor, BitConverter.GetBytes(NativeBattleTemporaryMemory.Address + 32));
        NativeBattleEffect345Frame frame = new(Memory(), new(345, 344, new(345, NativeBattleEffect345Profile.Entry, NativeBattleEffect345Profile.Initializer), null, null), new(1), NativeBattleEffect345State.FromColdProcess()) { Staging = staging };
        var result = NativeBattleEffect345Particles.Morph70ABF0(frame, basis, bytes.ToImmutableArray(), basis + 0x200, basis + 0x400, basis + 0x800, basis + 0x1000);
        Assert.Equal(new short[] { 500, 30, 40, 119 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Bank.AsSpan().Slice(0x208 + index * 2))).ToArray());
        Assert.Equal(2048u, result.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 500, 30, 40 }, result.Frame.Memory.ScriptedColorRegisters.Ir.ToArray());
        Assert.Equal(NativeBattleTemporaryMemory.Address + 32, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(2048, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 32, 4)));
        Assert.Equal(2048, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 36, 4)));
    }

    [Fact]
    public void Native_random_ranges_keep_conditional_shared_crt_draw_counts_and_negative_angle_edge()
    {
        NativeCrtRandomState initial = new(123);
        NativeBattleEffectNodeRandomValue constant = NativeBattleEffectNodeRandom.Range881F20(initial, 8, 8);
        Assert.Equal(initial, constant.Random);
        NativeCrtRandomState once = initial.Draw(out ushort sample);
        NativeBattleEffectNodeRandomValue reversed = NativeBattleEffectNodeRandom.Range881F20(initial, 12, -7);
        Assert.Equal(once, reversed.Random);
        Assert.Equal(12 - sample % -19, reversed.Value);
        NativeCrtRandomState twice = once.Draw(out ushort other);
        NativeBattleEffectNodeRandomValue product = NativeBattleEffectNodeRandom.ProductRange881F50(initial, -4, 9);
        Assert.Equal(twice, product.Random);
        Assert.Equal(-4 + unchecked(sample * other) % 13, product.Value);
        Assert.Equal(4096, NativeBattleEffectNodeRandom.Angle882030(initial, -4096, 0).Value);
    }

    [Fact]
    public void Sprite_branch_reads_retained_resource_and_keeps_first_selection_across_delivery()
    {
        const uint sourceAddress = 0x1643698;
        const int resource = 0x400;
        const int geometry = 0x870;
        byte[] bytes = new byte[18932];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), sourceAddress + 0x100);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(resource + 10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(resource + 12), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(resource + 16), 1);
        bytes[resource + 20] = 1;
        bytes[resource + 21] = 128;
        bytes[resource + 22] = 1;
        bytes[resource + 23] = 0x2C;
        NativeBattleNodeSource source = new(bytes.ToImmutableArray());
        (NativeBattleEffect345Frame frame, uint task) = Frame();
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(camera, 0) }, State = frame.State with { NodeData = ImmutableDictionary<uint, NativeBattleNodeSource>.Empty.Add(sourceAddress, source) } };
        frame = frame with { State = frame.State.WriteDword(0x255EA44, NativeBattleEffectNodeMemory.ArenaAddress).WriteDword(0x255EA88, NativeBattleEffectNodeMemory.ArenaAddress + geometry).WriteDword(0x255EA84, NativeBattleEffectNodeContextMemory.Address + 180).WriteDword(0x2557208, 0x2793E58) };
        frame = NativeBattleEffect345Controller.Write(frame, task, NativeBattleEffect345Controller.Read(frame, task).WriteDword(0x30, sourceAddress).WriteWord(0x54, 1).WriteWord(0x56, ushort.MaxValue).WriteDword(0x34 + 0x224, sourceAddress + 4));
        using (NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, task, 0, 0x870, geometry, 0x9D80))
        {
            memory.ContextDword(0x2C, memory.Pointer(geometry));
            memory.ContextWord(0x16, 2);
            for (int index = 0; index < 2; index++)
            {
                int node = geometry + index * 672;
                memory.Word(node + 8, 1);
                memory.Dword(node + 4, index == 0 ? memory.Pointer(geometry + 672) : 0);
                memory.Word(node + 0x1CC, 2);
                memory.Byte(node + 0x1D8, 1);
                memory.Dword(node + 0x170, sourceAddress + resource);
                for (int lane = 0; lane < 9; lane++)
                {
                    memory.Word(node + 0xAC + lane * 2, lane % 4 == 0 ? (ushort)4096 : (ushort)0);
                }
                memory.Dword(node + 0xC8, 4096);
            }
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            frame = frame with { Memory = written.Memory, Effect = written.Effect };
        }
        List<NativeBattleGfGeometryBatch> batches = [];
        NativeBattleEffect345Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => new(camera, 216, 160, 108), _ => 0, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _, _, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (current, operation) =>
        {
            NativeBattleEffect345DrawBatch drawn = Assert.IsType<NativeBattleEffect345DrawBatch>(operation);
            batches.Add(drawn.Batch);
            source.Write(resource + 20, [2]);
            return current;
        })
        {
            ReadModel = _ => throw new InvalidOperationException("Sprite delivery does not read an actor model."),
            ReadPoseSource = _ => throw new InvalidOperationException("Sprite delivery does not read an actor pose.")
        };
        NativeBattleEffect345Step result = NativeBattleEffect345Particles.Advance7075D0(frame, task, null!, inputs);
        Assert.Equal(2, batches.Count);
        NativeBattleGfGeometryAttempt first = Assert.Single(batches[0].Attempts);
        NativeBattleGfGeometryAttempt second = Assert.Single(batches[1].Attempts);
        Assert.NotNull(first.Primitive);
        Assert.Equal(first.Primitive!.Words, second.Primitive!.Words);
        Assert.Equal((byte)2, result.Frame.State.NodeData![sourceAddress].ReadByte(resource + 20));
    }

    private static (NativeBattleEffect345Frame Frame, uint Task) Frame()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x255D7F8, 0x255CD68, 676, 4);
        NativeBattleOriginalEffectState effect = new(345, 344, new(345, NativeBattleEffect345Profile.Entry, NativeBattleEffect345Profile.Initializer), null, null) { Pools = [pool] };
        return (new(Memory(), effect, new(1), NativeBattleEffect345State.FromColdProcess()), pool.BankAddress);
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
}
