using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePsychoBlast23ControllerTests
{
    [Fact]
    public void Entry_preserves_retained_basis_knownness_and_resets_only_entry_counters()
    {
        Fixture fixture = new();
        Assert.Equal(new ushort[] { 100, 88, 72, 676, 384, 176, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3, 1, 3, 50, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattlePsychoBlast23State retained = fixture.Frame.State with { Basis56 = fixture.Frame.State.Basis56.MarkUnknown(18, 2), Counter27044D0 = 123, Counter2703654 = 456, Nodes = fixture.Frame.State.Nodes! with { Counter26D76C0 = 8, PrimaryDrawCursor = 2345 } };
        NativeBattlePsychoBlast23Frame frame = NativeBattlePsychoBlast23Controller.Initialize(fixture.Frame.Memory, fixture.Frame.Effect, fixture.Frame.Random, fixture.Inputs.Packets, fixture.Resources, retained);
        Assert.Equal(0u, frame.State.Counter27044D0);
        Assert.Equal(0u, frame.State.Counter2703654);
        Assert.Equal((ushort)8, frame.State.Nodes!.Counter26D76C0);
        Assert.Equal(2345, frame.State.PrimaryDrawCursor);
        Assert.All(frame.State.Basis56.ReadSnapshot(18, 2).Known, known => Assert.False(known));
        frame = fixture.Step(frame);
        Assert.Equal(256 + 0x8000, frame.State.PrimaryDrawCursor);
        Assert.Equal(256 + 0x10000, frame.State.SecondaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256, frame.State.PrimaryDrawCursor);
        frame = fixture.Step(frame);
        Assert.Equal(256 + 0x10000, frame.State.Nodes!.Auxiliary25F8);
        Assert.Equal(256 + 0x10000 + 0x438, frame.State.Nodes.Auxiliary16F80);
        Assert.Equal(256 + 0x10000 + 0x438 + 0xD200, frame.State.Nodes.AuxiliaryCursor);
        Assert.Equal((ushort)0, frame.State.Nodes.Counter26D76C0);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, false)]
    public void Request_requires_both_current_and_requested_native_stages(short current, short pending, bool accepted)
    {
        Fixture fixture = new();
        NativeBattlePsychoBlast23State state = fixture.Frame.State.WithWord(0x40, current).WithWord(0x44, pending);
        Assert.Equal(accepted, NativeBattlePsychoBlast23Controller.Request(state, 0, 1, out NativeBattlePsychoBlast23State result));
        Assert.Equal(accepted ? (short)1 : pending, result.Word(0x44));
        Assert.Equal(current, result.Word(0x40));
    }

    [Fact]
    public void Shared_target_initialization_clears_origin_without_reading_caster_and_keeps_target_W()
    {
        Fixture fixture = new();
        NativeBattleModulePoolTask task = fixture.Frame.Effect.Pools[0].Slots[0].WriteByte(0x2D, 3);
        NativeBattlePsychoBlast23Inputs inputs = fixture.Inputs with { ReadModel = actor => actor == 3 ? new(3, 0) { Transform = new(new(0, 0, 0), new(120, -300, 400), unchecked((short)0xABCD), 0, 4096) } : throw new InvalidOperationException("Caster must not be read.") };
        NativeBattlePsychoBlast23State state = NativeBattlePsychoBlast23Controller.InitializeShared(fixture.Frame.State.WithWord(0, 300).WithWord(0x48, 1), task, inputs);
        Assert.Equal((short)0, state.Word(0));
        Assert.Equal((short)0, state.Word(2));
        Assert.Equal((short)0, state.Word(4));
        Assert.Equal((short)0, state.Word(0x48));
        Assert.Equal((short)120, state.Word(8));
        Assert.Equal(unchecked((short)0xABCD), state.Word(14));
    }

    [Fact]
    public void Director_propagates_in_two_callbacks_and_initializes_basis_before_spawning_actual_ACTs()
    {
        Fixture fixture = new();
        NativeBattlePsychoBlast23Frame frame = fixture.Frame;
        for (int index = 0; index < 5; index++)
        {
            frame = fixture.Step(frame);
        }
        Assert.Equal((short)1, frame.State.Word(0x44));
        Assert.Equal((short)1, frame.State.Word(0x42));
        Assert.Equal((short)0, frame.State.Word(0x40));
        Assert.Empty(frame.Effect.Pools[4].Slots.Where(task => task.Callback != 0));
        frame = fixture.Step(frame);
        Assert.Equal((short)1, frame.State.Word(0x40));
        Assert.Equal(2, frame.Effect.Pools[4].Slots.Count(task => task.Callback != 0));
        NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[3].Slots, task => task.Callback != 0);
        Assert.Equal((ushort)0, generic.ReadWord(0x298));
        Assert.Equal((ushort)4, generic.ReadWord(0x29C));
        Assert.Equal((ushort)40, generic.ReadWord(0x29E));
        Assert.Equal(new byte[] { 0, 0 }, frame.State.Basis56.Read(18, 2));
        Assert.Single(fixture.Operations.OfType<NativeBattlePsychoBlast23LookBasis>());
        Assert.Single(fixture.Operations.OfType<NativeBattlePsychoBlast23TargetAnchor>());
        NativeBattlePsychoBlast23Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePsychoBlast23Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(fixture.Step(frame)), NativeBattleTestSnapshots.Json(fixture.Step(restored)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Basis_transpose_preserves_destination_padding_and_translation(bool unknownPadding)
    {
        Fixture fixture = new();
        byte[] padding = [0xCD, 0xAB];
        byte[] translation = Enumerable.Range(1, 12).Select(value => (byte)value).ToArray();
        NativeBattleEffectArena basis = fixture.Frame.State.Basis56.Write(18, padding).Write(20, translation);
        if (unknownPadding)
        {
            basis = basis.MarkUnknown(18, 2);
        }
        NativeBattlePsychoBlast23Frame frame = fixture.Frame with { State = fixture.Frame.State with { Basis56 = basis } };
        for (int index = 0; index < 5; index++)
        {
            frame = fixture.Step(frame);
        }
        Assert.Single(fixture.Operations.OfType<NativeBattlePsychoBlast23LookBasis>());
        Assert.Equal(translation, frame.State.Basis56.Read(20, 12));
        if (unknownPadding)
        {
            Assert.All(frame.State.Basis56.ReadSnapshot(18, 2).Known, known => Assert.False(known));
            Assert.Throws<NotSupportedException>(() => frame.State.Basis56.Read(18, 2));
        }
        else
        {
            Assert.Equal(padding, frame.State.Basis56.Read(18, 2));
        }
    }

    internal sealed class Fixture
    {
        public NativeBattlePsychoBlast23Frame Frame { get; }
        public NativeBattlePsychoBlast23Resources Resources { get; }
        public NativeBattlePsychoBlast23Inputs Inputs { get; }
        public List<NativeBattlePsychoBlast23Operation> Operations { get; } = [];
        public Fixture()
        {
            var prior = NativeBattleRaldo19ControllerTests.Fixture();
            byte[] camera = new byte[832];
            BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), 832);
            byte[] sprite = new byte[248];
            sprite[3] = 6;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 9);
            for (int frame = 0; frame < 9; frame++)
            {
                int offset = 30 + 24 * frame;
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), (ushort)offset);
                BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(offset), 1);
                sprite[offset + 4] = 1;
                sprite[offset + 6] = 1;
                sprite[offset + 7] = 0x2C;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(28), ushort.MaxValue);
            Resources = new(new("camera", camera.ToImmutableArray()), new(new byte[15716].ToImmutableArray()) { ResourceAddress = 0x17EA4F0 }, 0x6427D, new(0x16146C8, [0xC0C0C0, 0x808080, 0x404040, 0xFF000000]), new(0, 0x161428C, 0, 0, sprite.ToImmutableArray()), Act(0x17EE254, 8028, 4, 32), Act(0x17F01B0, 5524, 2, 40), Act(0x17F1744, 8028, 4, 32));
            NativeBattleOriginalEffectState effect = new(23, 22, new(23, NativeBattlePsychoBlast23Profile.Entry, NativeBattlePsychoBlast23Profile.Initializer), prior.Frame.Effect.Parameters, null) { ResourcePointers = [new(NativeBattlePsychoBlast23Profile.InitializerGlobal, 256)] };
            NativeBattlePsychoBlast23State retained = NativeBattlePsychoBlast23State.FromColdProcess(new byte[84].ToImmutableArray(), new byte[56].ToImmutableArray(), new byte[672].ToImmutableArray(), 123, 456);
            Frame = NativeBattlePsychoBlast23Controller.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(22), effect, new(1), prior.Inputs.Packets, Resources, retained);
            Inputs = new(prior.Inputs.Packets, NativeBattlePackedMatrix32.FromMatrix(prior.Inputs.Camera, null), actor => prior.Inputs.ReadModel(actor) with { Rotation14 = 0 }, prior.Inputs.ReadPoseSource, Execute, (frame, _) => new(frame, 0), (frame, _) => new(frame, 0));
        }
        public NativeBattlePsychoBlast23Frame Step(NativeBattlePsychoBlast23Frame frame) => NativeBattlePsychoBlast23Controller.Advance(frame, Resources, Inputs);
        private NativeBattlePsychoBlast23Frame Execute(NativeBattlePsychoBlast23Frame frame, NativeBattlePsychoBlast23Operation operation)
        {
            Operations.Add(operation);
            if (operation is NativeBattlePsychoBlast23ActDraw draw)
            {
                NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
                NativeBattleModulePoolTask task = hierarchy.Read(draw.Address);
                hierarchy = hierarchy.Write(draw.Address, task.WriteDword(0x98, unchecked(task.ReadDword(0x98) + 1)));
                frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
            }
            return frame;
        }
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
