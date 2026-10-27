using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect20ControllerTests
{
    [Fact]
    public void Entry_uses_five_actual_pools_and_only_resets_its_two_entry_DWORDs()
    {
        Fixture fixture = new();
        Assert.Equal(new ushort[] { 100, 88, 120, 152, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 40, 100, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(0u, fixture.Frame.State.Counter27268F8);
        Assert.Equal(0u, fixture.Frame.State.Counter2722960);
        Assert.Equal(256 + 2 * 0xA000, fixture.Frame.State.AuxiliaryCursor);
        NativeBattleEffect20Frame frame = fixture.Step(fixture.Frame);
        Assert.Equal(256 + 0xA000, frame.State.PrimaryDrawCursor);
        Assert.Equal(256 + 2 * 0xA000, frame.State.SecondaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256, frame.State.PrimaryDrawCursor);
        Assert.Equal(256 + 0xA000, frame.State.SecondaryDrawCursor);
        Assert.Equal(fixture.Inputs.Camera, frame.Memory.SharedEffectView2793E58);
    }

    [Fact]
    public void Stage_fade_target_contact_and_flight_complete_through_their_distinct_operations()
    {
        Fixture fixture = new();
        NativeBattleEffect20Frame frame = fixture.Frame;
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = fixture.Step(frame);
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect20StageInitialize>());
        Assert.Equal(new short[] { 256, 512, 768, 1024, 768, 512, 256, 0 }, fixture.Operations.OfType<NativeBattleEffect20StageFade>().Select(operation => operation.Value));
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect20Contact>());
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect20VibrationEnqueue>());
        Assert.Equal((short)7, Assert.Single(fixture.Operations.OfType<NativeBattleEffect20VibrationStop>()).Handle);
        Assert.Equal(6, Assert.Single(fixture.Operations.OfType<NativeBattleEffect20Activate>()).Actor);
        NativeBattleEffect20RingSequence ring = Assert.Single(fixture.Operations.OfType<NativeBattleEffect20RingSequence>());
        Assert.Equal(0x1D97668u, ring.Address);
        Assert.Equal(12, ring.Count);
        int activate = fixture.Operations.FindIndex(operation => operation is NativeBattleEffect20Activate);
        Assert.IsType<NativeBattleEffect20VibrationStop>(fixture.Operations[activate + 1]);
        Assert.IsType<NativeBattleEffect20BodyDraw>(fixture.Operations[activate + 2]);
        int enqueue = fixture.Operations.FindIndex(operation => operation is NativeBattleEffect20VibrationEnqueue);
        Assert.IsType<NativeBattleEffect20ClearActorFlags124Bit2>(fixture.Operations[enqueue + 1]);
        Assert.IsType<NativeBattleEffect20BodyDraw>(fixture.Operations[enqueue + 2]);
    }

    [Fact]
    public void Target_release_at_exact_age_30_precedes_contact_on_its_next_phase()
    {
        Fixture fixture = new();
        NativeBattleEffect20Frame frame = fixture.Frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4).WriteByte(0x63, 1));
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, NativeBattleEffect20Profile.TargetCallback, 88, root, (_, _, _) => new(0, 3));
        hierarchy = target.Hierarchy.Write(target.Address, target.Hierarchy.Read(target.Address).WriteByte(0x29, 1).WriteWord(0x24, 29));
        frame = fixture.Step(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } });
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(root).ReadByte(0x63));
        frame = fixture.Step(frame);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(root).ReadByte(0x63));
        Assert.Empty(fixture.Operations.OfType<NativeBattleEffect20Contact>());
        frame = fixture.Step(frame);
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect20Contact>());
    }

    [Fact]
    public void Snapshot_preserves_private_cursors_task_physics_and_CRT_order()
    {
        Fixture fixture = new();
        NativeBattleEffect20Frame frame = fixture.Frame;
        for (int step = 0; step < 8; step++)
        {
            frame = fixture.Step(frame);
        }
        NativeBattleEffect20Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect20Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Step(frame))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Step(restored))));
    }

    internal sealed class Fixture
    {
        public NativeBattleEffect20Frame Frame { get; }
        public NativeBattleEffect20Resources Resources { get; }
        public NativeBattleEffect20Inputs Inputs { get; }
        public List<NativeBattleEffect20Operation> Operations { get; } = [];

        public Fixture()
        {
            var prior = NativeBattleRaldo19ControllerTests.Fixture();
            byte[] camera = new byte[180];
            BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), 180);
            byte[] mesh = new byte[96];
            BinaryPrimitives.WriteUInt32LittleEndian(mesh, 40);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 4);
            byte[] sprite = new byte[428];
            sprite[3] = 6;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
            for (int index = 0; index < 16; index++)
            {
                int offset = 44 + 24 * index;
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2), (ushort)offset);
                BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(offset), 1);
                sprite[offset + 4] = 1;
                sprite[offset + 6] = 1;
                sprite[offset + 7] = 0x2C;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(42), ushort.MaxValue);
            byte[] vibration = new byte[0x11A];
            BinaryPrimitives.WriteUInt16LittleEndian(vibration.AsSpan(0x100), 20);
            Resources = new(new("effect20-camera", camera.ToImmutableArray()), 0x6429B, new(0x1629F14, [0xC0C0C0, 0x808080, 0x404040, 0xFF000000]), [0, 6, 9, 16, 26, 28, 30, 32, 34, short.MaxValue], Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 12).ToImmutableArray(), new(0x17F36A0, new(new string('0', 64), [0, 0x100], vibration.ToImmutableArray())), new(0, 0x1629E00, mesh.ToImmutableArray()), new(0, 0x1629C54, 0, 0, sprite.ToImmutableArray()));
            NativeBattleOriginalEffectState effect = new(20, 19, new(20, NativeBattleEffect20Profile.Entry, NativeBattleEffect20Profile.Initializer), prior.Frame.Effect.Parameters, null) { ResourcePointers = [new(NativeBattleEffect20Profile.InitializerGlobal, 256)] };
            Frame = NativeBattleEffect20Controller.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(19), effect, new(1), prior.Inputs.Packets, Resources, NativeBattleEffect20State.FromColdProcess(123, 456));
            Inputs = new(prior.Inputs.Packets, NativeBattlePackedMatrix32.FromMatrix(prior.Inputs.Camera, null), prior.Inputs.ReadModel, prior.Inputs.ReadPoseSource, Execute, (frame, address) =>
            {
                NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
                NativeBattleModulePoolTask task = hierarchy.Read(address);
                hierarchy = hierarchy.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                return NativeBattleEffect20Particles.Finish(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, address);
            });
        }

        public NativeBattleEffect20Frame Step(NativeBattleEffect20Frame frame) => NativeBattleEffect20Controller.Advance(frame, Resources, Inputs);

        private NativeBattleEffect20Frame Execute(NativeBattleEffect20Frame frame, NativeBattleEffect20Operation operation)
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect20VibrationEnqueue enqueue)
            {
                NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
                hierarchy = hierarchy.Write(enqueue.Address, hierarchy.Read(enqueue.Address).WriteWord(0x74, 7));
                frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
            }
            return frame;
        }
    }
}
