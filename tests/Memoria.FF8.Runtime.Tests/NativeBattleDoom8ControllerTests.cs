using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ControllerTests
{
    [Fact]
    public void Root_two_waits_for_children_then_clears_exact_banks_and_four_word_counters()
    {
        Fixture fixture = new(2);
        NativeBattleDoom8Frame frame = fixture.Frame;
        const int allocation = 0x30000;
        frame = frame with
        {
            Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(allocation - 1, Enumerable.Repeat((byte)0xA5, 1080 + 26880 + 2).ToArray()) },
            State = frame.State.WriteDword(0x275F9D8, 0x20DFAB8 + allocation).WriteDword(0x275ABD8, 0xBEEF1234).WriteWord(0x275F508, 7).WriteWord(0x275F238, 8).WriteWord(0x2758508, 9)
        };
        frame = fixture.WriteRoot(frame, fixture.Root(frame).WriteByte(0x28, 1));
        NativeBattleDoom8Frame waiting = fixture.Step(frame);
        Assert.Equal((byte)2, fixture.Root(waiting).ReadByte(0x29));
        Assert.Equal((byte)0xA5, waiting.Memory.Arena!.Read(allocation, 1)[0]);
        frame = fixture.WriteRoot(waiting, fixture.Root(waiting).WriteByte(0x28, 0));
        frame = fixture.Step(frame);
        Assert.Equal((byte)3, fixture.Root(frame).ReadByte(0x29));
        Assert.Equal(0x20DFAB8u + allocation, frame.State.ReadDword(0x275F3EC));
        Assert.Equal(0x20DFAB8u + allocation + 1080, frame.State.ReadDword(0x275F980));
        Assert.Equal(0x20DFAB8u + allocation + 1080 + 26880, frame.State.ReadDword(0x275F9D8));
        Assert.All(frame.Memory.Arena!.Read(allocation, 1080 + 26880), value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(allocation - 1, 1)[0]);
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(allocation + 1080 + 26880, 1)[0]);
        Assert.Equal(0xBEEF0000u, frame.State.ReadDword(0x275ABD8));
        Assert.Equal((ushort)0, frame.State.ReadWord(0x275F508));
        Assert.Equal((ushort)0, frame.State.ReadWord(0x275F238));
        Assert.Equal((ushort)0, frame.State.ReadWord(0x2758508));
        Assert.Equal((ushort)2, fixture.Root(frame).ReadWord(0x24));
    }

    [Fact]
    public void Scan_order_is_physical_and_phase_six_reads_previous_survivors_before_reset()
    {
        Fixture fixture = new(6);
        NativeBattleDoom8Frame frame = fixture.WriteRoot(fixture.Frame, fixture.Root(fixture.Frame).WriteWord(0x5E, 1));
        int[] order = [1, 2, 9, 8, 3, 5, 6, 4, 7];
        foreach (int pool in order)
        {
            frame = fixture.Append(frame, pool, (uint)(0x100000 + pool), fixture.RootAddress, out _);
        }
        List<uint> visited = [];
        bool complete = false;
        fixture.Leaf = (current, address) =>
        {
            Assert.Equal((ushort)0, current.State.ReadWord(0x275F910));
            Assert.Equal((ushort)0, current.State.ReadWord(0x275F3E8));
            visited.Add(NativeBattleDoom8Controller.Read(current, address).Callback);
            if (complete)
            {
                NativeBattleEffectHierarchy hierarchy = new NativeBattleEffectHierarchy(current.Effect.Pools).Complete8DC530(address);
                current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            }
            return new(current, complete ? 2 : 0);
        };
        frame = fixture.Step(frame);
        Assert.Equal(order.Select(pool => (uint)(0x100000 + pool)), visited);
        Assert.Equal((byte)6, fixture.Root(frame).ReadByte(0x29));
        Assert.Equal((ushort)9, fixture.Root(frame).ReadWord(0x5E));
        complete = true;
        frame = fixture.Step(frame);
        Assert.Equal((byte)6, fixture.Root(frame).ReadByte(0x29));
        Assert.Equal((ushort)0, fixture.Root(frame).ReadWord(0x5E));
        frame = fixture.Step(frame);
        Assert.Equal((byte)7, fixture.Root(frame).ReadByte(0x29));
    }

    [Fact]
    public void Child_appended_to_a_later_pool_is_visible_in_the_same_scan()
    {
        Fixture fixture = new(6);
        NativeBattleDoom8Frame frame = fixture.Append(fixture.Frame, 1, 0x100001, fixture.RootAddress, out _);
        List<uint> calls = [];
        fixture.Leaf = (current, address) =>
        {
            uint callback = NativeBattleDoom8Controller.Read(current, address).Callback;
            calls.Add(callback);
            if (callback == 0x100001)
            {
                current = fixture.Append(current, 9, 0x100009, address, out _);
            }
            return new(current, 0);
        };
        frame = fixture.Step(frame);
        Assert.Equal(new uint[] { 0x100001, 0x100009 }, calls);
        Assert.Equal((ushort)2, fixture.Root(frame).ReadWord(0x5E));
    }

    [Fact]
    public void Target_consumes_director_contact_on_next_pass_then_parent_clear_on_a_later_root_phase()
    {
        Fixture fixture = new(4);
        NativeBattleDoom8Frame frame = fixture.WriteRoot(fixture.Frame, fixture.Root(fixture.Frame).WriteByte(0x63, 1).WriteWord(0x58, 0));
        frame = fixture.Append(frame, 1, NativeBattleDoom8Controller.TargetCallback, fixture.RootAddress, out uint target);
        frame = NativeBattleDoom8Controller.Write(frame, target, NativeBattleDoom8Controller.Read(frame, target).WriteByte(0x29, 1));
        frame = fixture.Append(frame, 2, NativeBattleDoom8Controller.DirectorCallback, target, out uint director);
        frame = NativeBattleDoom8Controller.Write(frame, director, NativeBattleDoom8Controller.Read(frame, director).WriteByte(0x29, 12));
        frame = frame with { State = frame.State.WithSharedWord(0x40, 5).WithSharedWord(0x42, 5).WithSharedWord(0x44, 5) };
        frame = fixture.Step(frame);
        Assert.Empty(fixture.Operations.OfType<NativeBattleDoom8Contact>());
        Assert.Equal((ushort)1, frame.State.SharedWord(0x48));
        frame = fixture.Step(frame);
        Assert.Single(fixture.Operations.OfType<NativeBattleDoom8Contact>());
        Assert.Equal((byte)0, NativeBattleDoom8Controller.Read(frame, target).ReadByte(0x28));
        frame = fixture.Step(frame);
        Assert.Equal((byte)3, NativeBattleDoom8Controller.Read(frame, target).ReadByte(0x29));
        frame = fixture.Step(frame);
        Assert.Equal((byte)4, fixture.Root(frame).ReadByte(0x29));
        Assert.Equal((byte)0, fixture.Root(frame).ReadByte(0x63));
        Assert.Equal((byte)0, fixture.Root(frame).ReadByte(0x28));
        frame = fixture.Step(frame);
        Assert.Equal((byte)5, fixture.Root(frame).ReadByte(0x29));
    }

    [Fact]
    public void Target_initialization_precedes_director_uploads_and_same_pass_IO_release_queues_but_does_not_complete()
    {
        Fixture fixture = new(3);
        NativeBattleDoom8Frame frame = fixture.Step(fixture.Frame);
        int initialize = fixture.Operations.FindIndex(operation => operation is NativeBattleDoom8InitializeTarget);
        int upload = fixture.Operations.FindIndex(operation => operation is NativeBattleDoom8UploadTim);
        Assert.True(initialize >= 0 && upload > initialize);
        NativeBattleModulePoolTask target = Assert.Single(frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0);
        Assert.Equal((byte)2, target.ReadByte(0x28));
        uint director = NativeBattleDoom8State.Pools[2].Bank;
        frame = NativeBattleDoom8Controller.Write(frame, director, NativeBattleDoom8Controller.Read(frame, director).WriteByte(0x29, 3));
        frame = fixture.Step(frame);
        NativeBattleDoom8QueueFile request = Assert.Single(fixture.Operations.OfType<NativeBattleDoom8QueueFile>());
        Assert.Equal((ushort)383, request.FileId);
        Assert.Equal(0x19EB018u, request.DestinationAddress);
        Assert.Equal((ushort)1, frame.State.ReadIoControlWord(0x164));
        Assert.Equal(0x1636C88u, frame.State.ReadDword(NativeBattleDoom8Io.CursorGlobal));
        Assert.Equal((byte)1, frame.Effect.Pools[9].Slots[0].ReadByte(0x29));
    }

    [Fact]
    public void Texture_request_waits_for_explicit_completion_and_JSON_resumes_identically()
    {
        Fixture fixture = new(7);
        NativeBattleDoom8Frame frame = fixture.Step(fixture.Frame);
        Assert.Single(fixture.Operations.OfType<NativeBattleDoom8TextureLoad>());
        Assert.Equal((byte)8, fixture.Root(frame).ReadByte(0x29));
        NativeBattleDoom8Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleDoom8Frame>(SnapshotJson(frame))!);
        Assert.Equal(SnapshotJson(fixture.Step(frame)), SnapshotJson(fixture.Step(restored)));
        frame = fixture.WriteRoot(frame, fixture.Root(frame).WriteByte(0x62, 1));
        frame = fixture.Step(frame);
        Assert.Equal((byte)9, fixture.Root(frame).ReadByte(0x29));
        frame = fixture.Step(frame);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Missing_required_leaf_services_throw_and_camera_parity_is_published_before_caster()
    {
        Fixture fixture = new(6);
        NativeBattleDoom8Frame independent = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Frame));
        NativeBattleDoom8Frame frame = fixture.Append(fixture.Frame, 8, 0x8CC3A0, fixture.RootAddress, out _);
        fixture.Leaf = (_, _) => throw new NotSupportedException("Reaper callback requires its actual implementation.");
        Assert.Throws<NotSupportedException>(() => fixture.Step(frame));
        fixture.Execute = (current, operation) =>
        {
            if (operation is NativeBattleDoom8UpdateCaster)
            {
                Assert.NotNull(current.Memory.SharedEffectView2793E58);
                Assert.Equal(current.State.ReadDword(0x275E828), current.State.ReadDword(0x2758504));
                Assert.Equal(0x2793E58u, current.State.ReadDword(0x275F92C));
            }
            return current;
        };
        fixture.Step(independent);
    }

    private sealed class Fixture
    {
        public NativeBattleDoom8Frame Frame { get; }
        public uint RootAddress { get; }
        public NativeBattleActionPacketsState Packets { get; }
        public List<NativeBattleDoom8Operation> Operations { get; } = [];
        public Func<NativeBattleDoom8Frame, uint, NativeBattleDoom8Step> Leaf { get; set; } = (_, _) => throw new NotSupportedException("Missing lower callback service.");
        public Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> Execute { get; set; }
        private readonly NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096), null);

        public Fixture(byte phase)
        {
            Packets = NativeBattleActionPacketsState.Fresh();
            (NativeBattleActionPacketsState packets, _) = Packets.Prepare(new(2, 2, 11, 0, 32, 15, null));
            (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            Packets = packets;
            NativeBattleEffectParameters parameters = new(0, 1, 8, 0, 0, 0);
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleDoom8Controller.RootCallback, 100);
            RootAddress = root.Address;
            hierarchy = root.Hierarchy.Write(RootAddress, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(RootAddress), parameters, Packets).WriteByte(0x29, phase));
            byte[] script = new byte[112];
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(16), 3);
            BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(18), 383);
            BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(24), 7072);
            NativeBattleDoom8State state = NativeBattleDoom8State.FromColdProcess(script.ToImmutableArray())
                .WriteDword(0x2758500, 0x20DFBB8).WriteDword(0x275E82C, 0x20DFBB8).WriteDword(0x275E828, 0x20E7BB8)
                .WriteDword(0x275DF4C, 0x20E7BB8).WriteDword(0x275DF48, 0x20EFBB8).WriteDword(0x275F9D8, 0x210FAB8);
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), parameters, NativeBattleDoom8State.Pools[0].Descriptor) { Pools = hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess().BeginLoad(8), effect, new(1), state);
            Execute = (current, operation) => current;
        }

        public NativeBattleModulePoolTask Root(NativeBattleDoom8Frame frame) => NativeBattleDoom8Controller.Read(frame, RootAddress);
        public NativeBattleDoom8Frame WriteRoot(NativeBattleDoom8Frame frame, NativeBattleModulePoolTask task) => NativeBattleDoom8Controller.Write(frame, RootAddress, task);

        public NativeBattleDoom8Frame Append(NativeBattleDoom8Frame frame, int pool, uint callback, uint parent, out uint address)
        {
            NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, NativeBattleDoom8State.Pools[pool].Stride, parent, (_, _, _) => new(0, 3));
            address = appended.Address;
            return frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
        }

        public NativeBattleDoom8Frame Step(NativeBattleDoom8Frame frame)
        {
            return NativeBattleDoom8Controller.Advance(frame, new(Packets, _ => camera, (current, operation) =>
            {
                Operations.Add(operation);
                return Execute(current, operation);
            }, (current, address) => Leaf(current, address), 0x494A2));
        }
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
