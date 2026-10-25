using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect31NodeTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, 0x26D2990)]
    [InlineData(2, 2)]
    public void Rejected_sprite_calls_publish_actual_walker_ebp_through_signed_counts_and_later_single_instances(int firstCount, int expectedSingleEbp)
    {
        const uint directoryAddress = NativeBattleEffect31Profile.CustomDirectoryAddress;
        const uint sourceAddress = 0x2000;
        const uint rotationAddress = 0x100000;
        const uint spriteAddress = 0x200000;
        byte[] directory = new byte[0x104];
        BinaryPrimitives.WriteUInt32LittleEndian(directory, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(4), sourceAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(0x44), spriteAddress);
        byte[] source = new byte[0x148];
        source[0x13] = 23;
        foreach (int offset in new[] { 0xD8, 0xDC, 0xE0 })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(offset), rotationAddress);
        }
        byte[] sprite = new byte[56];
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 9);
        for (int frameIndex = 0; frameIndex <= 9; frameIndex++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frameIndex * 2), 32);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(32), 1);
        sprite[36] = 1;
        sprite[37] = 0x80;
        sprite[38] = 1;
        sprite[39] = 0x2C;
        NativeBattleModuleResource module = new(0, spriteAddress, 0, 0, sprite.ToImmutableArray());
        NativeBattleEffect31Resources resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(spriteAddress, module), new Dictionary<uint, ImmutableArray<byte>>
        {
            [directoryAddress] = directory.ToImmutableArray(),
            [sourceAddress] = source.ToImmutableArray(),
            [sourceAddress + 0x148] = new byte[6].ToImmutableArray(),
            [sourceAddress + 0x150] = new byte[6].ToImmutableArray(),
            [rotationAddress] = [0, 0]
        }.ToImmutableDictionary());
        Fixture fixture = new();
        fixture.Memory.ContextDword(0x224, directoryAddress + 4);
        fixture.Memory.ContextDword(0x228, directoryAddress + 0x44);
        fixture.Memory.ContextWord(0x20, 1);
        fixture.Memory.ContextWord(0x1A, 100);
        fixture.Memory.ContextWord(0x16, 2);
        for (int index = 0; index < 2; index++)
        {
            int node = Fixture.Particle + index * NativeBattleEffect31Profile.ParticleStride;
            int count = index == 0 ? firstCount : 1;
            fixture.Memory.Link882930(node, 1);
            fixture.Memory.Byte(node + 0x1D7, 1);
            fixture.Memory.Byte(node + 0x1D8, unchecked((byte)count));
            fixture.Memory.Byte(node + 0x1D1, 8);
            fixture.Memory.Dword(node + 0x170, spriteAddress);
            for (int instance = 0; instance < Math.Max(1, count); instance++)
            {
                fixture.Memory.Dword(node + 0x12C + instance * 16 + 8, unchecked((uint)(-1 << 16)));
            }
        }
        NativeBattleEffectNodeMemoryWrite captured = fixture.Memory.FlushStorage();
        NativeBattleEffectMemory memory = captured.Memory with
        {
            GeometryRegisters = NativeBattleGeometryRegisters.Unknown(),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(),
            SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0)
        };
        NativeBattleEffect31State state = NativeBattleEffect31State.FromColdProcess().WriteDword(NativeBattleEffect31Profile.EmitterBankGlobal, NativeBattleEffect31Controller.CommonAddress).WriteDword(NativeBattleEffect31Profile.ParticleBankGlobal, NativeBattleEffect31Controller.CommonAddress + 0x438).WriteDword(NativeBattleEffect31Profile.ContextGlobal, 0x234).WriteDword(NativeBattleEffect31Profile.ScratchCursorGlobal, 0x2793E58).WriteDword(NativeBattleEffect31Profile.ViewGlobal, 0x2793E58);
        NativeBattleEffect31Frame frame = new(memory, captured.Effect, new(99), state) { Staging = NativeBattleFileStaging.Unknown().Write(directoryAddress, directory) };
        frame = NativeBattleEffect31Controller.Write(frame, 0x200, NativeBattleEffect31Controller.Read(frame, 0x200).WriteByte(0x29, 1).WriteDword(0x30, directoryAddress));
        List<uint> savedEbp = [];
        NativeBattleEffect31Inputs inputs = new(null!, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => new(null, 100, 160, 108), _ => 0, (_, _, _) => throw new InvalidOperationException(), (_, _, _, _, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            NativeBattleEffect31DrawBatch draw = Assert.IsType<NativeBattleEffect31DrawBatch>(operation);
            Assert.Equal(0, draw.Batch.AllocationBytes);
            Assert.NotEmpty(draw.Batch.Attempts);
            Assert.All(draw.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
            NativeBattleStackBytes stack = current.State.ReadCallbackStack();
            Assert.Equal(NativeBattleEffectNodeContextMemory.Address, stack.ReadUInt32(-0xE0));
            Assert.Equal(0x86F4DDu, stack.ReadUInt32(-0xD4));
            savedEbp.Add(stack.ReadUInt32(-0xDC));
            return current with { State = current.State.WriteDword(0x26D10A4, checked((uint)savedEbp.Count)) };
        });
        NativeBattleEffect31Step advanced = NativeBattleEffect31Nodes.Advance(frame, 0x200, resources, inputs);
        Assert.Equal(Math.Max(0, firstCount) + 1, savedEbp.Count);
        Assert.Equal(unchecked((uint)expectedSingleEbp), savedEbp[^1]);
        if (firstCount == 2)
        {
            Assert.Equal(new uint[] { 0, 1, 2 }, savedEbp);
        }
        Assert.Equal(checked((uint)savedEbp.Count), advanced.Frame.State.ReadDword(0x26D10A4));
        Assert.Equal(unchecked((uint)expectedSingleEbp), advanced.Frame.State.ReadCallbackStack().ReadUInt32(-0xDC));
        Assert.Equal(fixture.Memory.Pointer(Fixture.Particle + NativeBattleEffect31Profile.ParticleStride + 0xAC), advanced.Frame.State.ReadCallbackStack().ReadUInt32(-0xD8));
    }

    [Fact]
    public void Context_start_and_completion_preserve_the_outer_callback_clock_and_child_barrier()
    {
        Fixture fixture = new();
        NativeBattleEffectNodeMemoryWrite captured = fixture.Memory.FlushStorage();
        NativeBattleEffect31Frame frame = new(captured.Memory, captured.Effect, new(99), NativeBattleEffect31State.FromColdProcess());
        frame = NativeBattleEffect31Controller.Write(frame, 0x200, NativeBattleEffect31Controller.Read(frame, 0x200).WriteByte(0x29, 1).WriteByte(0x28, 1));
        NativeBattleEffect31Step first = NativeBattleEffect31Nodes.Advance(frame, 0x200, null!, null!);
        Assert.Equal(0, first.Result);
        Assert.Equal((ushort)1, NativeBattleEffect31Controller.Read(first.Frame, 0x200).ReadWord(0x54));
        Assert.Equal((ushort)1, NativeBattleEffect31Controller.Read(first.Frame, 0x200).ReadWord(0x24));
        frame = NativeBattleEffect31Controller.Write(first.Frame, 0x200, NativeBattleEffect31Controller.Read(first.Frame, 0x200).WriteWord(0x54, 2));
        NativeBattleEffect31Step pending = NativeBattleEffect31Nodes.Advance(frame, 0x200, null!, null!);
        Assert.Equal(0, pending.Result);
        Assert.Equal((byte)2, NativeBattleEffect31Controller.Read(pending.Frame, 0x200).ReadByte(0x29));
        Assert.Equal((byte)1, NativeBattleEffect31Controller.Read(pending.Frame, 0x200).ReadByte(0x26));
        frame = NativeBattleEffect31Controller.Write(pending.Frame, 0x200, NativeBattleEffect31Controller.Read(pending.Frame, 0x200).WriteByte(0x28, 0));
        NativeBattleEffect31Step completed = NativeBattleEffect31Nodes.Advance(frame, 0x200, null!, null!);
        Assert.Equal(2, completed.Result);
        Assert.Equal((ushort)3, NativeBattleEffect31Controller.Read(completed.Frame, 0x200).ReadWord(0x24));
        Assert.Equal(new NativeCrtRandomState(99), completed.Frame.Random);
    }

    [Fact]
    public void Emitter_only_pass_publishes_rotation_feedback_and_retires_before_completion_is_observed()
    {
        const uint sourceAddress = 0x2000;
        const uint countAddress = 0x100000;
        const uint directoryAddress = NativeBattleEffect31Profile.CustomDirectoryAddress;
        byte[] directory = new byte[0x104];
        BinaryPrimitives.WriteUInt32LittleEndian(directory, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(4), sourceAddress);
        byte[] source = new byte[0x148];
        source[0x12] = 1;
        source[0x25] = 1;
        source[0x36] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(source, 3 << 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 4 << 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), -5 << 16);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0xC8), countAddress);
        NativeBattleEffect31Resources resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, new Dictionary<uint, ImmutableArray<byte>>
        {
            [directoryAddress] = directory.ToImmutableArray(),
            [sourceAddress] = source.ToImmutableArray(),
            [sourceAddress + 0x148] = new byte[6].ToImmutableArray(),
            [sourceAddress + 0x150] = new byte[6].ToImmutableArray(),
            [countAddress] = [0]
        }.ToImmutableDictionary());
        Fixture fixture = new();
        fixture.Memory.ContextDword(0x224, directoryAddress + 4);
        fixture.Memory.ContextDword(0x228, directoryAddress + 0x44);
        fixture.Memory.ContextWord(0x20, 1);
        fixture.Memory.ContextWord(0x1A, 1);
        NativeBattleEffectNodeMemoryWrite captured = fixture.Memory.FlushStorage();
        ImmutableArray<byte?> anchor = Enumerable.Repeat<byte?>(0xA5, 8).ToImmutableArray();
        NativeBattleEffectMemory memory = captured.Memory with
        {
            GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Translation = [7, 8, 9], AnchorScratch8 = anchor },
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0)
        };
        NativeBattleEffect31State state = NativeBattleEffect31State.FromColdProcess().WriteDword(NativeBattleEffect31Profile.EmitterBankGlobal, NativeBattleEffect31Controller.CommonAddress).WriteDword(NativeBattleEffect31Profile.ParticleBankGlobal, NativeBattleEffect31Controller.CommonAddress + 0x438).WriteDword(NativeBattleEffect31Profile.ContextGlobal, 0x234).WriteDword(NativeBattleEffect31Profile.ScratchCursorGlobal, 0x2793E58).WriteDword(NativeBattleEffect31Profile.ViewGlobal, 0x2793E58);
        NativeBattleEffect31Frame frame = new(memory, captured.Effect, new(99), state) { Staging = NativeBattleFileStaging.Unknown().Write(directoryAddress, directory) };
        frame = NativeBattleEffect31Controller.Write(frame, 0x200, NativeBattleEffect31Controller.Read(frame, 0x200).WriteByte(0x29, 1).WriteDword(0x30, directoryAddress));
        NativeBattleEffect31Inputs inputs = new(null!, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => new(null, 100, 160, 108), _ => 0, (_, _, _) => throw new InvalidOperationException(), (_, _, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        NativeBattleEffect31Step advanced = NativeBattleEffect31Nodes.Advance(frame, 0x200, resources, inputs);
        Assert.Equal(0, advanced.Result);
        Assert.Equal((ushort)2, NativeBattleEffect31Controller.Read(advanced.Frame, 0x200).ReadWord(0x54));
        Assert.Equal(new int?[] { 3, 4, -5 }, advanced.Frame.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 7, 8, 9 }, advanced.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal(anchor, advanced.Frame.Memory.GeometryRegisters.AnchorScratch8);
        Assert.Equal(new byte[] { 3, 0, 4, 0, 0xFB, 0xFF }, advanced.Frame.Memory.ScriptedColorRegisters.NormalInput!.Value.Take(6));
        Assert.False(advanced.Frame.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.False(advanced.Frame.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal((ushort)1, advanced.Frame.State.ReadWord(NativeBattleEffect31Profile.EmitterIndexGlobal));
        Assert.Equal((ushort)0, advanced.Frame.State.ReadWord(NativeBattleEffect31Profile.ParticleIndexGlobal));
        Assert.Equal(new NativeCrtRandomState(99), advanced.Frame.Random);
        Assert.Equal(2, NativeBattleEffect31Nodes.Advance(advanced.Frame, 0x200, resources, inputs).Result);
    }

    [Fact]
    public void Source_operands_read_only_the_three_consumed_fragments_for_each_selected_index()
    {
        Fixture fixture = new();
        foreach (sbyte index in new sbyte[] { 0, 1, 2, 3, 5, 7 })
        {
            byte[] physical = fixture.AddSource(index);
            physical[0x12] = unchecked((byte)(10 + index));
            BinaryPrimitives.WriteInt16LittleEndian(physical.AsSpan(0x148), unchecked((short)(100 + index)));
            BinaryPrimitives.WriteInt16LittleEndian(physical.AsSpan(0x154), unchecked((short)(200 + index)));
            fixture.Reads.Clear();
            byte[] source = fixture.Lifecycle.ReadSource(index);
            uint address = fixture.SourceAddress(index);
            Assert.Equal(new[] { (address, 0x148), (address + 0x148, 6), (address + 0x150, 6) }, fixture.Reads);
            Assert.Equal(0x156, source.Length);
            Assert.Equal(10 + index, source[0x12]);
            Assert.Equal(100 + index, BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x148)));
            Assert.Equal(200 + index, BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x154)));
            physical[0x150] = 0x71;
            Assert.Equal(0x71, fixture.Lifecycle.ReadSource(index)[0x150]);
            Assert.Throws<NotSupportedException>(() => fixture.Bank.Read(address + 0x14E, 2));
            Assert.Throws<NotSupportedException>(() => fixture.Bank.Read(address + 0x156, 2));
        }
        Assert.Equal(fixture.Directory, fixture.Bank.Data.ToArray());
    }

    [Fact]
    public void Selected_sprite_emission_consumes_live_count_radius_rotation_and_both_euler_fragments()
    {
        Fixture fixture = new();
        byte[] physical = fixture.AddSource(0);
        physical[0x12] = 12;
        physical[0x25] = 1;
        physical[0x34] = 1;
        physical[0x36] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(physical.AsSpan(0x148), 111);
        BinaryPrimitives.WriteInt16LittleEndian(physical.AsSpan(0x14A), 222);
        BinaryPrimitives.WriteInt16LittleEndian(physical.AsSpan(0x14C), 333);
        uint count = fixture.AddChannel(physical, 0xC8, [0]);
        uint[] radii = [fixture.AddChannel(physical, 0xCC, [0, 0]), fixture.AddChannel(physical, 0xD0, [0, 0]), fixture.AddChannel(physical, 0xD4, [0, 0])];
        uint[] rotations = [fixture.AddChannel(physical, 0xD8, [0, 0]), fixture.AddChannel(physical, 0xDC, [0, 0]), fixture.AddChannel(physical, 0xE0, [0, 0])];
        fixture.AddSprite(0, 9);
        fixture.Memory.Byte(0x6A, 0);
        NativeCrtRandomState random = new(123);
        fixture.Lifecycle.InitializeEmitter(0, fixture.Lifecycle.ReadSource(0), ref random);
        fixture.Lifecycle.EmitGeometry(0, fixture.Lifecycle.ReadSource(0), ref random);
        Assert.Equal(0, fixture.GeometryAllocations);
        fixture.Current(count)[0] = 1;
        fixture.Lifecycle.EmitGeometry(0, fixture.Lifecycle.ReadSource(0), ref random);
        Assert.Equal(1, fixture.GeometryAllocations);
        Assert.Equal((ushort)111, fixture.Memory.Word(Fixture.Particle + 0x194));
        Assert.Equal((ushort)222, fixture.Memory.Word(Fixture.Particle + 0x196));
        Assert.Equal((ushort)333, fixture.Memory.Word(Fixture.Particle + 0x198));
        NativeCrtRandomState expected = new NativeCrtRandomState(123).Draw(out _).Draw(out _).Draw(out _);
        Assert.Equal(expected, random);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(fixture.Current(rotations[axis]), unchecked((short)(axis + 1)));
        }
        fixture.Lifecycle.AdvanceMotion(Fixture.Particle, fixture.Lifecycle.ReadSource(0));
        Assert.Equal((ushort)112, fixture.Memory.Word(Fixture.Particle + 0xCC));
        Assert.Equal((ushort)224, fixture.Memory.Word(Fixture.Particle + 0xCE));
        Assert.Equal((ushort)336, fixture.Memory.Word(Fixture.Particle + 0xD0));
        Assert.Contains((count, 1), fixture.Reads);
        foreach (uint channel in radii.Concat(rotations))
        {
            Assert.Contains((channel, 2), fixture.Reads);
        }
        NativeBattleEffectNodeMemoryWrite written = fixture.Memory.FlushStorage();
        Assert.Equal(fixture.Directory, fixture.Bank.Data.ToArray());
        Assert.Equal(0x438 + 0x8340, written.Memory.Arena!.Read(0, 0x438 + 0x8340).Length);
    }

    [Fact]
    public void Animated_anchor_reads_each_current_selector_and_signed_word_channel_without_caching_bss_values()
    {
        Fixture fixture = new();
        byte[] physical = fixture.AddSource(1);
        physical[0x11] = 4;
        physical[0x12] = 24;
        uint[] selectors = new uint[4];
        int[] selectorFields = [0x100, 0x104, 0x108, 0x10C];
        for (int index = 0; index < selectors.Length; index++)
        {
            selectors[index] = fixture.AddChannel(physical, selectorFields[index], [0]);
        }
        Dictionary<int, uint> words = [];
        foreach (int field in new[] { 0x110, 0x114, 0x118, 0x11C, 0x120, 0x124, 0x128 })
        {
            words.Add(field, fixture.AddChannel(physical, field, [0, 0]));
        }
        BinaryPrimitives.WriteInt16LittleEndian(fixture.Current(words[0x128]), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(fixture.Current(words[0x11C]), -7);
        fixture.Memory.Byte(0x6A, 1);
        NativeCrtRandomState random = new(42);
        fixture.Lifecycle.InitializeEmitter(0, fixture.Lifecycle.ReadSource(1), ref random);
        Assert.Equal(unchecked((uint)(-7 << 16)), fixture.Memory.Dword(0x4C));
        BinaryPrimitives.WriteInt16LittleEndian(fixture.Current(words[0x11C]), 19);
        NativeBattleEffectAnimatedAnchor.Advance(fixture.Memory, 0, fixture.Lifecycle.ReadSource(1), fixture.Bank.Read);
        Assert.Equal(19u << 16, fixture.Memory.Dword(0x4C));
        Assert.Equal(new NativeCrtRandomState(42), random);
        foreach (uint selector in selectors)
        {
            Assert.Contains((selector, 1), fixture.Reads);
        }
        foreach (uint word in words.Values)
        {
            Assert.Contains((word, 2), fixture.Reads);
        }
        fixture.Current(selectors[0])[0] = 2;
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectAnimatedAnchor.Advance(fixture.Memory, 0, fixture.Lifecycle.ReadSource(1), fixture.Bank.Read));
    }

    private sealed class Fixture
    {
        internal const int Particle = 0x438;
        private readonly Dictionary<uint, byte[]> regions = [];
        private uint nextChannel = 0x100000;
        private ushort particleCursor;
        internal byte[] Directory { get; } = new byte[0x80];
        internal List<(uint Address, int Length)> Reads { get; } = [];
        internal NativeBattleEffectNodeMemory Memory { get; }
        internal NativeBattleEffectNodeBank Bank { get; }
        internal NativeBattleEffectNodeLifecycle Lifecycle { get; }
        internal int GeometryAllocations { get; private set; }

        internal Fixture()
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 676, 1);
            (pool, int slot) = pool.Append(0x86D1D0);
            pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[664])) };
            NativeBattleOriginalEffectState effect = new(31, 30, new(31, 0x86C9E0, 0x86C9C0), null, null) { Pools = [pool] };
            Memory = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, 0x200, 0, 0x438, Particle, 0x8340);
            Memory.ContextDword(0x224, 0x1000);
            Memory.ContextDword(0x228, 0x1040);
            Bank = new(0x1000, Directory.ToImmutableArray(), Read);
            NativeBattleEffectNodeInputs inputs = new(null, _ => throw new InvalidOperationException("Unreached model join."), (_, _, _) => throw new InvalidOperationException("Unreached emitter allocation."), (emitter, source) =>
            {
                GeometryAllocations++;
                return NativeBattleEffectNodeAllocation.AllocateGeometry(Memory, Particle, 50, ref particleCursor, emitter, source);
            });
            Lifecycle = new(Memory, Bank, inputs);
        }

        internal uint SourceAddress(sbyte index) => unchecked(0x2000u + (uint)index * 0x200);

        internal byte[] AddSource(sbyte index)
        {
            uint address = SourceAddress(index);
            byte[] physical = new byte[0x156];
            BinaryPrimitives.WriteUInt32LittleEndian(Directory.AsSpan(index * 4), address);
            Bank.ReplaceData(Directory.ToImmutableArray());
            regions.Add(address, physical);
            return physical;
        }

        internal uint AddChannel(byte[] source, int field, byte[] bytes)
        {
            uint address = nextChannel;
            nextChannel += 0x100;
            regions.Add(address, bytes);
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(field), address);
            return address;
        }

        internal void AddSprite(sbyte index, ushort frames)
        {
            const uint address = 0x200000;
            byte[] bytes = new byte[10];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), frames);
            regions.Add(address, bytes);
            BinaryPrimitives.WriteUInt32LittleEndian(Directory.AsSpan(0x40 + index * 4), address);
            Bank.ReplaceData(Directory.ToImmutableArray());
        }

        internal byte[] Current(uint address) => regions[address];

        private byte[] Read(uint address, int length)
        {
            foreach ((uint start, byte[] bytes) in regions)
            {
                if (address < start || (ulong)address + (uint)length > (ulong)start + (uint)bytes.Length)
                {
                    continue;
                }
                int offset = checked((int)(address - start));
                if (start < 0x100000 && !((offset == 0 && length == 0x148) || (offset == 0x148 && length == 6) || (offset == 0x150 && length == 6)))
                {
                    throw new NotSupportedException("Unclaimed source padding cannot be read.");
                }
                Reads.Add((address, length));
                return bytes.AsSpan(offset, length).ToArray();
            }
            throw new NotSupportedException("The current addressed channel is unknown.");
        }
    }
}
