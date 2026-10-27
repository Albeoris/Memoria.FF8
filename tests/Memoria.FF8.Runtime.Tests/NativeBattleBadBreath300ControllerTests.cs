using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBadBreath300Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath300ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_parity_retains_native_first_bank_and_has_no_unreached_services(uint parity, int offset, uint next)
    {
        Fixture fixture = new();
        NativeBattleEffectTaskPool root = fixture.Frame.Effect.Pools[0];
        NativeBattleBadBreath300Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteDword(16, parity)) })] } };
        NativeBattleBadBreath300Frame result = fixture.Step(frame);
        Assert.Equal(ArenaAddress + (uint)offset, result.State.ReadDword(CursorGlobal));
        Assert.Equal(next, result.Effect.Pools[0].Slots[0].ReadDword(16));
        Assert.Empty(fixture.Operations);
        Assert.Single(result.Effect.Pools);
        Assert.Null(result.TargetScaleStack);
    }

    [Fact]
    public void Actual_main_target_particle_timing_runs_required_draw_services_and_releases_all_pools()
    {
        Fixture fixture = new();
        NativeBattleBadBreath300Frame frame = fixture.Frame;
        int callbacks = 0;
        while (frame.Effect.ReturnedDescriptor != 0 && callbacks++ < 130)
        {
            frame = fixture.Step(frame);
        }
        Assert.InRange(callbacks, 60, 129);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(new ushort[] { 20, 60, 320, 24 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 1, 1, 4, 32 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(100, fixture.Operations.OfType<NativeBattleBadBreath300DrawStrip>().Count());
        Assert.Equal(Enumerable.Range(0, 50), fixture.Operations.OfType<NativeBattleBadBreath300Clip>().Select(operation => operation.Phase));
        Assert.Single(fixture.Operations.OfType<NativeBattleBadBreath300Contact>());
        Assert.Equal(0, fixture.Operations.OfType<NativeBattleBadBreath300Contact>().Single().Index);
        Assert.Single(fixture.Operations.OfType<NativeBattleBadBreath300Sound>());
        Assert.Equal(162, fixture.Operations.OfType<NativeBattleBadBreath300DrawParticle>().Count());
        Assert.True(fixture.ScaleGates.Count > 0);
        Assert.All(fixture.ScaleGates, gate => Assert.Equal(17u, gate));
        Assert.All(frame.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Active_callback_JSON_restore_recreates_current_stack_producer_and_identical_next_state()
    {
        Fixture first = new();
        NativeBattleBadBreath300Frame frame = first.Frame;
        for (int callback = 0; callback < 38; callback++)
        {
            frame = first.Step(frame);
        }
        NativeBattleBadBreath300Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBadBreath300Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Fixture second = new();
        second.CopyCallbackFrom(first);
        first.Operations.Clear();
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Step(frame)), NativeBattleTestSnapshots.Json(second.Step(restored)));
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Operations), NativeBattleTestSnapshots.Json(second.Operations));
    }

    [Fact]
    public void Normalization_reads_the_actual_164_samples_and_only_overwrites_packed_W()
    {
        byte[] bytes = Mesh().ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x534), 32767);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x53C), -32768);
        ImmutableArray<byte> result = NativeBattleBadBreath300Controller.Normalize6B7CB0(bytes.ToImmutableArray());
        for (int index = 0; index < bytes.Length; index++)
        {
            bool w = index >= 14 && index < 1328 && ((index - 14) & 7) < 2;
            if (!w)
            {
                Assert.Equal(bytes[index], result[index]);
            }
        }
        Assert.Equal((short)((-16 << 12) / (32767 + 2640)), BinaryPrimitives.ReadInt16LittleEndian(result.AsSpan().Slice(14)));
    }

    [Fact]
    public void Deformation_publishes_last_actual_MVMVA_and_preserves_unknown_packed_W()
    {
        Fixture fixture = new();
        NativeBattleBadBreath300Frame frame = fixture.Frame;
        NativeBattleEffectArena arena = frame.Memory.Arena!.MarkUnknown(ScratchOffset, ScratchLength);
        frame = frame with { Memory = frame.Memory with { Arena = arena, GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, 0) } };
        NativeBattleBadBreath300Frame result = NativeBattleBadBreath300Controller.Deform66FBB0(frame, MeshAAddress, 73, fixture.Resources);
        for (int index = 0; index < 165; index++)
        {
            (byte[] _, bool[] known) = result.Memory.Arena!.ReadSnapshot(ScratchOffset + index * 8, 8);
            Assert.All(known.Take(6), value => Assert.True(value));
            Assert.All(known.Skip(6), value => Assert.False(value));
        }
        Assert.Equal(-2640, result.Memory.ScriptedColorRegisters!.Ir[2]);
        Assert.Null(result.Memory.GeometryRegisters!.Sxy[2]);
        Assert.Null(result.Memory.ScriptedColorRegisters.Material);
    }

    private static ImmutableArray<byte> Mesh()
    {
        byte[] bytes = new byte[5248];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1328);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 165);
        for (int index = 0; index < 165; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), (short)(-16 * (index + 1)));
        }
        return bytes.ToImmutableArray();
    }

    private sealed class Fixture
    {
        public NativeBattleBadBreath300Frame Frame { get; }
        public NativeBattleBadBreath300Resources Resources { get; }
        public List<NativeBattleBadBreath300Operation> Operations { get; } = [];
        public List<uint> ScaleGates { get; } = [];
        private NativeBattleActionPacketsState Packets { get; }
        private NativeBattlePoseSource Pose { get; }
        private NativeBattleGraphicsModel Model { get; }
        private long callback;

        public Fixture()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 0, 300, null));
            (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 4), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            Packets = packets;
            byte[] skeleton = new byte[112];
            skeleton[0] = 2;
            Pose = new(skeleton.ToImmutableArray(), [1]);
            Model = new(3, 2) { Flags8 = 0, MinimumY146 = 40, Transform = new(new(0, 0, 0), new(0, 100, 100), 0, 0, 4096) };
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, _) = root.Append(RootCallback);
            NativeBattleOriginalEffectState effect = new(300, 300, new(300, Entry, Initializer), new(0, 1, 8, 0, 0, 0), RootDescriptor) { Pools = [root] };
            ImmutableArray<byte> mesh = Mesh();
            NativeBattleBadBreath300State state = new(NativeBattleEffectArena.Clear(), mesh, mesh);
            Frame = new(NativeBattleEffectMemory.FromColdProcess().BeginLoad(300), effect, new(1), state.WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D972C0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            byte[] source = new byte[80];
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 32);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(32), 1);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(36), 26);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(40), 16);
            Resources = new(new(0x1042304, source.ToImmutableArray()), new(0, 0x103F824, 0, 0, []), new("fixture", []), new("fixture", []), mesh, mesh, new short[8192].ToImmutableArray(), [0, 0, 0, 0], new byte[8].ToImmutableArray(), 0x55781);
        }

        public void CopyCallbackFrom(Fixture source)
        {
            callback = source.callback;
        }

        public NativeBattleBadBreath300Frame Step(NativeBattleBadBreath300Frame frame) => NativeBattleBadBreath300Controller.Advance(frame, Resources, new(Packets, actor => actor < 3 ? Model with { Index = actor, Flags0 = 0 } : Model, _ => Pose, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0x1234), (current, _) => new(current, new(0, 100, 100, 0)), current => new(current, new(17, 100, 0)), (current, _, _, _) => new(current, new(0, 100, 100)), (current, operation) =>
        {
            Operations.Add(operation);
            return current;
        }, (current, _, context, _) =>
        {
            ScaleGates.Add(BinaryPrimitives.ReadUInt32LittleEndian(context.Read(20, 4)));
            return current;
        }, ++callback));
    }
}
