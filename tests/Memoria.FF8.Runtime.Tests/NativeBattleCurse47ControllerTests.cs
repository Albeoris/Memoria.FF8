using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47ControllerTests
{
    [Theory]
    [InlineData(0, 0x20E3AB8u, 0x20E7AB8u)]
    [InlineData(1, 0x20DFAB8u, 0x20E3AB8u)]
    public void Root_parity_selects_original_banks_before_caster_and_copies_the_complete_camera(ushort parity, uint primary, uint secondary)
    {
        Fixture fixture = new(0);
        NativeBattleCurse47Frame frame = fixture.PutRoot(fixture.Frame, fixture.Root(fixture.Frame).WriteWord(0x5C, parity));
        fixture.Execute = (current, operation) =>
        {
            if (operation is NativeBattleCurse47UpdateCaster)
            {
                Assert.Equal(primary, current.State.ReadDword(0x264B3CC));
                Assert.Equal(secondary, current.State.ReadDword(0x264B4D4));
                Assert.Equal(0x2793E58u, current.State.ReadDword(0x264EADC));
                Assert.Equal(0x2793E58u, current.State.ReadDword(0x264B4D8));
                Assert.Equal(JsonSerializer.Serialize(fixture.Camera), JsonSerializer.Serialize(current.Memory.SharedEffectView2793E58));
            }
            return current;
        };
        NativeBattleCurse47Frame result = fixture.Step(frame);
        Assert.Single(fixture.Operations);
        Assert.Equal((ushort)(parity + 1), fixture.Root(result).ReadWord(0x5C));
        Assert.Equal((ushort)1, fixture.Root(result).ReadWord(0x24));
        Assert.Equal((byte)1, fixture.Root(result).ReadByte(0x29));
    }

    [Fact]
    public void Phase_two_waits_for_children_then_clears_only_the_native_emitter_and_geometry_extents()
    {
        Fixture fixture = new(2);
        NativeBattleCurse47Frame waiting = fixture.PutRoot(fixture.Frame, fixture.Root(fixture.Frame).WriteByte(0x28, 1));
        waiting = fixture.Step(waiting);
        Assert.Equal((byte)2, fixture.Root(waiting).ReadByte(0x29));
        Assert.Equal(0u, waiting.State.ReadDword(0x264E708));
        const int offset = 0x10000;
        NativeBattleEffectArena arena = fixture.Frame.Memory.Arena!.Write(offset - 1, new byte[] { 0xAB }).Write(offset + 0x438 + 0xB7C0, new byte[] { 0xCD });
        NativeBattleCurse47Frame result = fixture.Step(fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = arena } });
        Assert.Equal((byte)3, fixture.Root(result).ReadByte(0x29));
        Assert.Equal(0x20EFAB8u, result.State.ReadDword(0x264E708));
        Assert.Equal(0x20EFEF0u, result.State.ReadDword(0x264EAE0));
        Assert.Equal(0x20FB6B0u, result.State.ReadDword(0x264EAE8));
        Assert.Equal(new byte[] { 0xAB }, result.Memory.Arena!.Read(offset - 1, 1));
        Assert.Equal(new byte[] { 0xCD }, result.Memory.Arena.Read(offset + 0x438 + 0xB7C0, 1));
        Assert.All(result.Memory.Arena.Read(offset, 0x438 + 0xB7C0), value => Assert.Equal((byte)0, value));
        foreach (uint address in new uint[] { 0x264D828, 0x264E714, 0x264E6A4, 0x264B3D0 })
        {
            Assert.Equal((ushort)0, result.State.ReadWord(address));
        }
    }

    [Fact]
    public void Same_pass_target_initialization_precedes_director_and_IO_and_keeps_the_mutable_script_owner()
    {
        Fixture fixture = new(3);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Frame);
        NativeBattleEffectTaskPool targets = result.Effect.Pools[1];
        NativeBattleModulePoolTask target = targets.Slots[0];
        Assert.Equal(NativeBattleCurse47Controller.TargetCallback, target.Callback);
        Assert.Equal((byte)1, target.ReadByte(0x29));
        Assert.Equal((byte)2, target.ReadByte(0x28));
        Assert.Equal((byte)4, fixture.Root(result).ReadByte(0x29));
        Assert.Equal((ushort)3, fixture.Root(result).ReadWord(0x5E));
        Assert.Equal((byte)1, result.Effect.Pools[2].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)1, result.State.SharedWord(0x44));
        Assert.Equal((byte)0, result.Effect.Pools[7].Slots[0].ReadByte(0x29));
        Assert.Equal(0x20F9AB8u, result.State.ReadDword(NativeBattleCurse47Io.NextAddressGlobal));
        Assert.Equal(new[] { typeof(NativeBattleCurse47UpdateCaster), typeof(NativeBattleCurse47UpdateTargetBounds), typeof(NativeBattleCurse47InitializeTarget) }, fixture.Operations.Select(operation => operation.GetType()));
    }

    [Fact]
    public void Root_scans_all_seven_child_banks_in_native_order_and_consumes_frame_returning_callbacks()
    {
        Fixture fixture = new(0);
        NativeBattleCurse47Frame frame = fixture.Frame;
        foreach (int pool in new[] { 1, 2, 7, 3, 5, 4, 6 })
        {
            frame = fixture.Append(frame, pool, 0x900000u + (uint)pool, out _);
        }
        List<uint> callbacks = [];
        fixture.Leaf = (current, address) =>
        {
            callbacks.Add(NativeBattleCurse47Controller.Read(current, address).Callback);
            return new(current with { State = current.State.WithSharedWord(0x10, unchecked((ushort)(current.State.SharedWord(0x10) + 1))) }, 0);
        };
        NativeBattleCurse47Frame result = fixture.Step(frame);
        Assert.Equal(new uint[] { 0x900001, 0x900002, 0x900007, 0x900003, 0x900005, 0x900004, 0x900006 }, callbacks);
        Assert.Equal((ushort)7, result.State.SharedWord(0x10));
        Assert.Equal((ushort)7, fixture.Root(result).ReadWord(0x5E));
    }

    [Theory]
    [InlineData(0xFF, 0, 0, 3)]
    [InlineData(0, 1, 0, 3)]
    [InlineData(1, 1, 0, 5)]
    [InlineData(0, 1, 1, 4)]
    public void Root_repeat_uses_signed_header_index_and_explicit_target_release(byte index, ushort maximum, byte held, byte phase)
    {
        Fixture fixture = new(4);
        NativeBattleCurse47Frame frame = fixture.PutRoot(fixture.Frame, fixture.Root(fixture.Frame).WriteByte(0x2A, index).WriteWord(0x58, maximum).WriteByte(0x63, held));
        NativeBattleCurse47Frame result = fixture.Step(frame);
        Assert.Equal(phase, fixture.Root(result).ReadByte(0x29));
        Assert.Equal(phase == 3 ? unchecked((byte)(index + 1)) : index, fixture.Root(result).ReadByte(0x2A));
    }

    [Fact]
    public void Target_contact_waits_for_shared_signal_and_releases_root_before_its_actual_children_finish()
    {
        Fixture fixture = new(4);
        NativeBattleCurse47Frame frame = fixture.Append(fixture.Frame, 1, NativeBattleCurse47Controller.TargetCallback, out uint target);
        frame = NativeBattleCurse47Controller.Write(frame, target, NativeBattleCurse47Controller.Read(frame, target).WriteByte(0x29, 1));
        frame = fixture.PutRoot(frame, fixture.Root(frame).WriteByte(0x63, 1));
        frame = fixture.Step(frame);
        Assert.Empty(fixture.Operations.OfType<NativeBattleCurse47Contact>());
        frame = fixture.Step(frame with { State = frame.State.WithSharedWord(0x48, 1) });
        Assert.Equal(0, Assert.Single(fixture.Operations.OfType<NativeBattleCurse47Contact>()).Index);
        Assert.Equal((byte)2, NativeBattleCurse47Controller.Read(frame, target).ReadByte(0x29));
        frame = NativeBattleCurse47Controller.Write(frame, target, NativeBattleCurse47Controller.Read(frame, target).WriteByte(0x28, 1));
        frame = fixture.Step(frame with { State = frame.State.WithSharedWord(0x4C, 1) });
        Assert.Equal((byte)3, NativeBattleCurse47Controller.Read(frame, target).ReadByte(0x29));
        frame = fixture.Step(frame);
        Assert.Equal((byte)0, fixture.Root(frame).ReadByte(0x63));
        Assert.Equal((byte)4, NativeBattleCurse47Controller.Read(frame, target).ReadByte(0x29));
        Assert.Equal((byte)1, NativeBattleCurse47Controller.Read(frame, target).ReadByte(0x28));
    }

    [Fact]
    public void Texture_completion_and_terminal_cold_next_require_real_completion_instead_of_elapsed_time()
    {
        Fixture fixture = new(7);
        NativeBattleCurse47Frame frame = fixture.Step(fixture.Frame);
        Assert.Equal(0x20DFAB8u, Assert.Single(fixture.Operations.OfType<NativeBattleCurse47TextureLoad>()).SourceAddress);
        Assert.Equal((byte)8, fixture.Root(frame).ReadByte(0x29));
        NativeBattleCurse47Frame restored = JsonSerializer.Deserialize<NativeBattleCurse47Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(fixture.Step(frame)), JsonSerializer.Serialize(fixture.Step(restored)));
        frame = fixture.PutRoot(frame, fixture.Root(frame).WriteByte(0x62, 1));
        frame = fixture.Step(frame);
        Assert.Equal((byte)9, fixture.Root(frame).ReadByte(0x29));
        frame = fixture.Step(frame);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.PutRoot(fixture.Frame, fixture.Root(fixture.Frame).WriteByte(0x29, 0xFF))));
    }

    private sealed class Fixture
    {
        public NativeBattleCurse47Frame Frame { get; }
        public NativeBattleActionPacketsState Packets { get; }
        public uint Address { get; }
        public NativeBattlePackedMatrix32 Camera { get; } = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096), 0x1357);
        public List<NativeBattleCurse47Operation> Operations { get; } = [];
        public Func<NativeBattleCurse47Frame, NativeBattleCurse47Operation, NativeBattleCurse47Frame> Execute { get; set; } = (frame, _) => frame;
        public Func<NativeBattleCurse47Frame, uint, NativeBattleCurse47Step> Leaf { get; set; } = (_, _) => throw new NotSupportedException("Actual lower callback required.");
        public Fixture(byte phase)
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(2, 2, 11, 0, 32, 15, null));
            (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            Packets = packets;
            NativeBattleEffectParameters parameters = new(0, 1, 47, 0, 0, 0);
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleCurse47Controller.RootCallback, 100);
            Address = root.Address;
            hierarchy = root.Hierarchy.Write(Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(Address), parameters, packets).WriteByte(0x29, phase));
            NativeBattleCurse47State state = NativeBattleCurse47IoTests.Initialized().WriteDword(0x264B3C8, 0x20DFAB8).WriteDword(0x264E69C, 0x20DFAB8).WriteDword(0x264E698, 0x20E3AB8).WriteDword(0x264DFDC, 0x20E3AB8).WriteDword(0x264DFD8, 0x20E7AB8).WriteDword(0x264EAE8, 0x20EFAB8);
            NativeBattleOriginalEffectState effect = new(47, 47, new(47, NativeBattleCurse47Controller.Entry, 0x82A7D0), parameters, NativeBattleCurse47State.Pools[0].Descriptor) { Pools = hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess().BeginLoad(47), effect, new(1), state);
        }
        public NativeBattleModulePoolTask Root(NativeBattleCurse47Frame frame) => NativeBattleCurse47Controller.Read(frame, Address);
        public NativeBattleCurse47Frame PutRoot(NativeBattleCurse47Frame frame, NativeBattleModulePoolTask task) => NativeBattleCurse47Controller.Write(frame, Address, task);
        public NativeBattleCurse47Frame Append(NativeBattleCurse47Frame frame, int pool, uint callback, out uint address)
        {
            NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, NativeBattleCurse47State.Pools[pool].Stride, Address, (_, _, _) => new(0, 3));
            address = appended.Address;
            return frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
        }
        public NativeBattleCurse47Frame Step(NativeBattleCurse47Frame frame) => NativeBattleCurse47Controller.Advance(frame, new(Packets, _ => Camera, (current, operation) =>
        {
            Operations.Add(operation);
            return Execute(current, operation);
        }, (current, address) => Leaf(current, address), 0x64221));
    }
}
