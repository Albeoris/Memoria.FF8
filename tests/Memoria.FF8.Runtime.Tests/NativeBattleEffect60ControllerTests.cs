using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect60ControllerTests
{
    [Fact]
    public void Entry_preserves_module_bytes_and_uses_original_seven_pools_and_shared_node_allocations()
    {
        Fixture fixture = new();
        Assert.Equal(new ushort[] { 100, 88, 72, 676, 396, 140, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3, 2, 3, 50, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleEffect60State retained = fixture.Frame.State with { SavedRotation8 = fixture.Frame.State.SavedRotation8.MarkUnknown(6, 2), MorphScratch = fixture.Frame.State.MorphScratch.Write(966, new byte[] { 0xCD, 0xAB }), Counter2607050 = 123, Counter2605DD4 = 456 };
        NativeBattleEffect60Frame frame = NativeBattleEffect60Controller.Initialize(fixture.Frame.Memory, fixture.Frame.Effect, fixture.Frame.Random, fixture.Inputs.Packets, fixture.Resources, retained);
        Assert.Equal(0u, frame.State.Counter2607050);
        Assert.Equal(0u, frame.State.Counter2605DD4);
        Assert.Throws<NotSupportedException>(() => frame.State.SavedRotation8.Read(6, 2));
        Assert.Equal(new byte[] { 0xCD, 0xAB }, frame.State.MorphScratch.Read(966, 2));
        for (int index = 0; index < 3; index++)
        {
            frame = fixture.Step(frame);
        }
        Assert.Equal(256 + 0x10000, frame.State.Nodes!.Auxiliary25F8);
        Assert.Equal(256 + 0x10000 + 0x438, frame.State.Nodes.Auxiliary16F80);
        Assert.Equal(256 + 0x10000 + 0x438 + 0xD200, frame.State.Nodes.AuxiliaryCursor);
        Assert.Equal(256 + 0x8000, frame.State.PrimaryDrawCursor);
    }

    [Fact]
    public void Shared_initialization_copies_actual_caster_and_target_positions_with_unknown_W()
    {
        Fixture fixture = new();
        fixture.Positions[0] = Fixture.Packed(100, 200, 300, 0xABCD).MarkUnknown(6, 2);
        fixture.Positions[3] = Fixture.Packed(-100, -200, -300, 0x1234);
        NativeBattleModulePoolTask task = fixture.Frame.Effect.Pools[0].Slots[0].WriteByte(0x2C, 0).WriteByte(0x2D, 3);
        NativeBattleEffect60State state = NativeBattleEffect60Controller.InitializeShared(fixture.Frame.State.WithWord(0x48, 1), task, fixture.Inputs);
        Assert.Equal((short)100, state.Word(0));
        Assert.Equal((short)-100, state.Word(8));
        Assert.Throws<NotSupportedException>(() => state.Word(6));
        Assert.Equal((short)0x1234, state.Word(14));
        Assert.Equal((short)0, state.Word(0x48));
    }

    [Fact]
    public void Director_spawns_two_addressed_generic_tasks_distinct_ACT_and_stage_and_sets_contact_at_age36()
    {
        Fixture fixture = new();
        NativeBattleEffect60Frame frame = fixture.Frame;
        for (int index = 0; index < 6; index++)
        {
            frame = fixture.Step(frame);
        }
        NativeBattleModulePoolTask[] generic = frame.Effect.Pools[3].Slots.Where(task => task.Callback != 0).ToArray();
        Assert.Equal(2, generic.Length);
        Assert.Contains(generic, task => task.Callback == NativeBattleEffect60Profile.GenericPrimaryCallback && task.ReadDword(0x30) == 0x1797CC0 && task.ReadWord(0x298) == 6);
        Assert.Contains(generic, task => task.Callback == NativeBattleEffect60Profile.GenericSecondaryCallback && task.ReadDword(0x30) == 0x179C998 && task.ReadWord(0x298) == 36);
        Assert.All(generic, task => Assert.Equal((ushort)40, task.ReadWord(0x29E)));
        Assert.Single(frame.Effect.Pools[4].Slots, task => task.Callback == NativeBattleEffect60Profile.ActPrimaryCallback);
        Assert.Single(frame.Effect.Pools[5].Slots, task => task.Callback == NativeBattleEffect60Profile.StageCallback);
        Assert.Single(frame.Effect.Pools[5].Slots, task => task.Callback == NativeBattleEffect60Profile.DebrisEmitterCallback);
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect60Sound>());
        Assert.Equal((short)0, frame.State.Word(0x48));
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask director = Assert.Single(frame.Effect.Pools[2].Slots, task => task.Callback != 0);
        uint address = hierarchy.Address(2, Array.FindIndex(frame.Effect.Pools[2].Slots.ToArray(), task => task.Callback != 0));
        hierarchy = hierarchy.Write(address, director.WriteByte(0x29, 3));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools }, State = frame.State.WithWord(0x46, 35) };
        frame = fixture.Step(frame);
        Assert.Equal((short)1, frame.State.Word(0x48));
    }

    [Fact]
    public void Snapshot_preserves_sparse_shared_rotation_morph_and_canonical_camera_convention()
    {
        Fixture fixture = new();
        NativeBattleEffect60Frame frame = fixture.Step(fixture.Frame);
        frame = frame with { State = frame.State with { Shared84 = frame.State.Shared84.MarkUnknown(6, 2), SavedRotation8 = frame.State.SavedRotation8.MarkUnknown(6, 2), MorphScratch = frame.State.MorphScratch.MarkUnknown(966, 2) } };
        NativeBattleEffect60Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect60Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Step(frame))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Step(restored))));
        Assert.Null(restored.State.Nodes!.CopiedCamera);
        Assert.NotNull(restored.Memory.SharedEffectView2793E58);
    }

    internal sealed class Fixture
    {
        public NativeBattleEffect60Frame Frame { get; }
        public NativeBattleEffect60Resources Resources { get; }
        public NativeBattleEffect60Inputs Inputs { get; }
        public List<NativeBattleEffect60Operation> Operations { get; } = [];
        public Dictionary<int, NativeBattleEffectArena> Positions { get; } = [];
        public Dictionary<int, NativeBattleEffectArena> Rotations { get; } = [];
        public Fixture()
        {
            var prior = NativeBattleRaldo19ControllerTests.Fixture();
            for (int actor = 0; actor < 7; actor++)
            {
                Positions[actor] = Packed((short)(actor * 100), 0, (short)(actor * 300), 0xABCD);
                Rotations[actor] = Packed(11, 22, 33, 0x1234).MarkUnknown(6, 2);
            }
            byte[] camera = new byte[788];
            BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), 788);
            byte[] sprite = new byte[428];
            sprite[3] = 6;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
            for (int frame = 0; frame < 16; frame++)
            {
                int offset = 44 + 24 * frame;
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), (ushort)offset);
                BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(offset), 1);
                sprite[offset + 4] = 1;
                sprite[offset + 6] = 1;
                sprite[offset + 7] = 0x2C;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(42), ushort.MaxValue);
            Resources = new(new("camera", camera.ToImmutableArray()), new(new byte[19672].ToImmutableArray()) { ResourceAddress = 0x1797CC0 }, new(new byte[17260].ToImmutableArray()) { ResourceAddress = 0x179C998 }, Act(0x17A0D04, 9292, 4, 32), Act(0x17A3150, 3200, 1, 24), new(0, 0x1597360, 0, 0, sprite.ToImmutableArray()), 0x64280, [0, 2407, 3476, 3706, 3883, 4007, 4077, 4096]);
            NativeBattleOriginalEffectState effect = new(60, 59, new(60, NativeBattleEffect60Profile.Entry, NativeBattleEffect60Profile.Initializer), prior.Frame.Effect.Parameters, null) { ResourcePointers = [new(NativeBattleEffect60Profile.InitializerGlobal, 256)] };
            Frame = NativeBattleEffect60Controller.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(59), effect, new(1), prior.Inputs.Packets, Resources, NativeBattleEffect60State.FromColdProcess(new byte[84].ToImmutableArray(), new byte[8].ToImmutableArray(), new byte[968].ToImmutableArray(), 123, 456));
            Inputs = new(prior.Inputs.Packets, NativeBattlePackedMatrix32.FromMatrix(prior.Inputs.Camera, null), actor =>
            {
                NativeBattleEffectArena position = Positions[actor];
                return prior.Inputs.ReadModel(actor) with { Rotation14 = Word(Rotations[actor], 2), Transform = new(new(0, 0, 0), new(Word(position, 0), Word(position, 2), Word(position, 4)), 0, 0, 4096) };
            }, prior.Inputs.ReadPoseSource, Execute, (frame, _) => new(frame, 0))
            {
                ReadPackedPosition8 = actor => Positions[actor],
                ReadRotation8 = actor => Rotations[actor],
                ReadWorld32 = _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)
            };
        }
        public NativeBattleEffect60Frame Step(NativeBattleEffect60Frame frame) => NativeBattleEffect60Controller.Advance(frame, Resources, Inputs);
        private NativeBattleEffect60Frame Execute(NativeBattleEffect60Frame frame, NativeBattleEffect60Operation operation)
        {
            Operations.Add(operation);
            switch (operation)
            {
                case NativeBattleEffect60SetPackedPosition position:
                    Positions[position.Actor] = position.Packed8;
                    break;
                case NativeBattleEffect60RestoreRotation rotation:
                    Rotations[rotation.Actor] = rotation.Packed8;
                    break;
                case NativeBattleEffect60SetYaw yaw:
                    Rotations[yaw.Actor] = PutWord(Rotations[yaw.Actor], 2, yaw.Value);
                    break;
                case NativeBattleEffect60ActDraw draw:
                    NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
                    NativeBattleModulePoolTask task = hierarchy.Read(draw.Address);
                    hierarchy = hierarchy.Write(draw.Address, task.WriteDword(0x98, unchecked(task.ReadDword(0x98) + 1)));
                    frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
                    break;
            }
            return frame;
        }
        public static NativeBattleEffectArena Packed(short x, short y, short z, ushort w)
        {
            byte[] bytes = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), z);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), w);
            return NativeBattleEffectArena.Clear().Write(0, bytes);
        }
        public static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, short value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
            return arena.Write(offset, bytes);
        }
        public static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        private static NativeBattleGfAnimationSource Act(uint address, int length, int count, int duration)
        {
            byte[] bytes = new byte[length];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), count);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), duration);
            for (int index = 0; index < count; index++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), 56 + index * 8);
            }
            return new(address, bytes.ToImmutableArray());
        }
    }
}
