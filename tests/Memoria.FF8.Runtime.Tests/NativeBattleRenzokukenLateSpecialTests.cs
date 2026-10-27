using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokukenLateSpecialTests
{
    [Fact]
    public void Rebound_target_sequence_uses_the_callback_entry_pointer_as_its_initial_accumulator()
    {
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, 0xC3, 0x1A, 0xE5, 0x17, 0xA1]) { SequenceSectionAddress = 0xE55D68, AnimationFrames = [1] };
        NativeBattleGraphicsModel model = new(3, 2)
        {
            Flags2 = 8,
            Sequence = 1,
            SequenceFrame = 0,
            ResourceFlags44 = 1,
            SequenceState = new() { Offset46 = 2, Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 }
        };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Equal(unchecked((short)0xE55D6E), step.Model.Rotation14);
        Assert.Equal((ushort)5, step.Model.SequenceState!.Offset46);
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceMachine.Advance(model, resource with { SequenceSectionAddress = null }, new(null, null)));
    }

    [Theory]
    [InlineData(339)]
    [InlineData(340)]
    public void Target_sequence_precedes_resource_replacement_without_restarting_it(int selected)
    {
        ushort effect = (ushort)selected;
        NativeBattleRenzokuken160Frame frame = Frame(effect);
        List<NativeBattleRenzokuken160Operation> operations = [];
        NativeBattleRenzokuken160ChildStep first = Controller(frame, 1, operations);
        NativeBattleRenzokukenSpecialTargetSequence sequence = Assert.Single(operations.OfType<NativeBattleRenzokukenSpecialTargetSequence>());
        Assert.Equal((byte)3, sequence.Target);
        Assert.Equal((byte)1, sequence.Sequence);
        Assert.Empty(operations.OfType<NativeBattleRenzokukenSpecialTargetResource>());
        Assert.Empty(operations.OfType<NativeBattleRenzokuken160ActorResource>());

        operations.Clear();
        Controller(first.Frame, 2, operations);
        NativeBattleRenzokuken160ActorResource actor = Assert.Single(operations.OfType<NativeBattleRenzokuken160ActorResource>());
        Assert.Equal(effect, actor.Resource.MagicId);
        NativeBattleRenzokukenSpecialTargetResource[] targets = operations.OfType<NativeBattleRenzokukenSpecialTargetResource>().ToArray();
        Assert.Equal(effect == 339 ? 2 : 1, targets.Length);
        Assert.All(targets, target => Assert.False(target.StartSequence));
        Assert.Equal((byte)3, targets[0].Target);
        Assert.Equal((byte)0, targets[0].Bank);
        if (effect == 339)
        {
            Assert.Equal((byte)4, targets[1].Target);
            Assert.Equal((byte)1, targets[1].Bank);
        }
        Assert.Empty(operations.OfType<NativeBattleRenzokukenSpecialTargetSequence>());
        Assert.True(operations.IndexOf(actor) < operations.IndexOf(targets[0]));
    }

    [Fact]
    public void Delayed_binding_reads_AB_duration_on_the_following_callback()
    {
        NativeBattleRenzokuken160Frame frame = Frame(339);
        NativeBattleRenzokuken160ChildStep bound = Controller(frame, 2, []);
        Assert.Equal(200, NativeBattleRenzokukenSpecialController.ReadState(bound.Frame).Core.Duration);
        NativeBattleRenzokuken160ChildStep scanned = Controller(bound.Frame, 3, []);
        Assert.Equal(60, NativeBattleRenzokukenSpecialController.ReadState(scanned.Frame).Core.Duration);
        Assert.Equal(new byte[] { 10, 20, 30 }, NativeBattleRenzokukenSpecialController.ReadState(scanned.Frame).Core.AbSegments);
    }

    [Fact]
    public void Native342_retains_the_unsigned_AB_total_plus_its_35_callback_tail()
    {
        NativeBattleRenzokuken160ChildStep step = Controller(Frame(342), 2, []);
        Assert.Equal(95, NativeBattleRenzokukenSpecialController.ReadState(step.Frame).Core.Duration);
        Assert.Equal(new uint[] { 0x5777F0, 0x578350, 0x5788D0, 0x578A00 }, step.Pool.Slots.Take(4).Select(task => task.Callback));
    }

    [Fact]
    public void Native339_late_tasks_share_the_physical_row_bank_and_preserve_rng_order()
    {
        NativeBattleRenzokuken160Frame frame = Frame(339);
        for (int row = 0; row < 3; row++)
        {
            frame = NativeBattleRenzokukenSpecialController.SetExtraPaddingUnknown(frame, row, true);
        }
        NativeBattleRenzokuken160ChildStep step = Controller(frame, 72, []);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort first).Draw(out ushort second);
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(new uint[] { 0x57E6F0, 0x57FF90, 0x580310, 0x5804A0 }, step.Pool.Slots.Take(4).Select(task => task.Callback));
        Assert.Equal((ushort)8, step.Pool.Slots[1].ReadWord(26));
        Assert.Equal((first % 1024 + 2560) / 3, step.Pool.Slots[2].ReadWord(28));
        Assert.Equal((ushort)5632, step.Pool.Slots[3].ReadWord(28));
        Assert.Equal((second % 1024 + 2560) / 3, step.Pool.Slots[3].ReadWord(32));
        byte[] storage = NativeBattleRenzokukenSpecialController.ReadState(step.Frame).ExtraStorage.ToArray();
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(storage));
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(storage.AsSpan().Slice(32)));
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(storage.AsSpan().Slice(64)));
        Assert.Equal(-2816, Word(storage, 26));
        Assert.Equal(1024, Word(storage, 28));
        Assert.Equal(-4096, Word(storage, 32 + 28));
        Assert.All(Enumerable.Range(0, 3), row => Assert.False(NativeBattleRenzokukenSpecialController.ReadExtraPaddingUnknown(step.Frame, row)));
    }

    [Fact]
    public void Native343_captures_the_reached_actor_matrix_after_its_single_scale_draw()
    {
        NativeBattleRenzokuken160Frame frame = Frame(343);
        NativeBattleRenzokuken160ChildStep step = Controller(frame, 55, []);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort scale);
        Assert.Equal(expected, step.Frame.Random);
        Assert.Same(Identity, NativeBattleRenzokukenSpecialController.ReadState(step.Frame).CapturedMatrix);
        Assert.Equal(0x577240u, step.Pool.Slots[1].Callback);
        Assert.Equal((scale % 3072 + 7168) / 3, step.Pool.Slots[1].ReadWord(28));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void Native342_additional_particles_follow_only_contact_indexes_one_through_six(int contact, bool additional)
    {
        NativeBattleRenzokuken160Frame frame = Frame(342);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleRenzokukenSpecialProfiles.Renzokuken342.ChildDescriptor, NativeBattleRenzokukenSpecialProfiles.Renzokuken342.ChildBank, 36, 100);
        (pool, int slot) = pool.Append(0x578A00);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, checked((ushort)(10 * (contact + 1))))) });
        NativeBattleRenzokuken160Inputs inputs = Inputs(Enumerable.Repeat((byte)10, 9).ToImmutableArray(), []);
        NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokukenSpecialController.Advance578A00(frame, pool, slot, inputs, NativeBattleRenzokukenSpecialController.ReadState(frame).Resources);
        Assert.Equal(additional ? 1 : 0, step.Pool.Slots.Count(task => task.Callback == 0x5790D0));
        Assert.Equal(1, step.Pool.Slots.Count(task => task.Callback == 0x578DF0));
    }

    [Fact]
    public void Native342_contact_three_reverses_all_debris_velocity_components_without_extra_random_draws()
    {
        NativeBattleRenzokuken160Frame original = Frame(342);
        (NativeBattleRenzokuken160ChildStep First, NativeBattleRenzokuken160ChildStep Fourth) steps = (Contact(0), Contact(3));
        Assert.Equal(steps.First.Frame.Random, steps.Fourth.Frame.Random);
        NativeCrtRandomState expected = original.Random;
        for (int draw = 0; draw < 121; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, steps.First.Frame.Random);
        ImmutableArray<byte> first = NativeBattleRenzokukenSpecialController.ReadState(steps.First.Frame).Core.Debris;
        ImmutableArray<byte> fourth = NativeBattleRenzokukenSpecialController.ReadState(steps.Fourth.Frame).Core.Debris;
        for (int particle = 0; particle < 12; particle++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int offset = particle * 20 + 12 + 2 * axis;
                Assert.Equal(unchecked((short)-BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan().Slice(offset))), BinaryPrimitives.ReadInt16LittleEndian(fourth.AsSpan().Slice(offset)));
            }
        }

        NativeBattleRenzokuken160ChildStep Contact(int contact)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleRenzokukenSpecialProfiles.Renzokuken342.ChildDescriptor, NativeBattleRenzokukenSpecialProfiles.Renzokuken342.ChildBank, 36, 100);
            (pool, int slot) = pool.Append(0x578A00);
            pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, checked((ushort)(10 * (contact + 1))))) });
            return NativeBattleRenzokukenSpecialController.Advance578A00(original, pool, slot, Inputs(Enumerable.Repeat((byte)10, 9).ToImmutableArray(), []), NativeBattleRenzokukenSpecialController.ReadState(original).Resources);
        }
    }

    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 11, 22, 33, 4096);

    [Theory]
    [InlineData(300, 0)]
    [InlineData(299, 1)]
    public void Native343_skips_unavailable_streak_rows_before_consuming_their_random_values(int occupied, int admitted)
    {
        NativeBattleModuleState modules = NativeBattleModuleState.Clear();
        for (int index = 0; index < occupied; index++)
        {
            (modules, _) = modules.AppendCallback(index == 0 ? 0x575BA0u : 0x123456u);
        }
        byte[] payload = NativeBattleStorageSnapshots.Capture(modules.Slots[0]).Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, 4);
        modules = modules with { Slots = modules.Slots.SetItem(0, modules.Slots[0] with { Payload = payload.ToImmutableArray() }) };
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        int bones = 0;
        NativeBattleModuleInputs inputs = new(0)
        {
            CrtRandom = random,
            ReadModelAnchor = _ => new(0, 0, 0, 0),
            ReadModelBoneCount = _ =>
            {
                bones++;
                return 1;
            },
            ReadModelBone = (_, _, _) => new(100, -200, 300)
        };
        NativeBattleModuleCallback result = NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(modules, 0, inputs, 0x575E30, true);
        NativeCrtRandomState expected = random;
        for (int index = 0; index < 8 * admitted; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, result.CrtRandomAfter);
        Assert.Equal(admitted, bones);
        Assert.Equal(admitted, result.State.Slots.Count(task => task.Callback == 0x575E30));
        Assert.Equal((short)5, BinaryPrimitives.ReadInt16LittleEndian(NativeBattleStorageSnapshots.Capture(result.State.Slots[0]).Payload.AsSpan()));
        if (admitted == 0)
        {
            Assert.Throws<InvalidOperationException>(() => NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(modules, 0, inputs));
        }
    }

    private static NativeBattleRenzokuken160Frame Frame(ushort effect)
    {
        NativeBattleRenzokukenSpecialProfile profile = NativeBattleRenzokukenSpecialProfiles.ForEffect(effect);
        BattleNativeRenzokukenResource actor = new(effect, profile.Entry, 0, [], [], new([]), 0, 1, [], new([], 0))
        {
            TargetResources = Enumerable.Range(0, effect == 339 ? 2 : 1).Select(_ => new BattleNativeRenzokukenTargetResource(0, [], [], new([]))).ToImmutableArray()
        };
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), actor, [], [], new int[10].ToImmutableArray(), [], 0, 0) { ImpactAxis = [0, 0, -4096], ContactOffsets = new NativeBattleVector[10].ToImmutableArray() };
        NativeBattleRenzokuken160State core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 200, Origin = new(100, -200, 300), Direction = new(0, 0, -4096) };
        NativeBattleRenzokukenSpecialState state = new(core, resources) { ExtraStorage = new byte[3200].ToImmutableArray(), ContactOffsets = new NativeBattleVector[10].ToImmutableArray() };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { RenzokukenSpecials = ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty.Add(effect, state) };
        NativeBattleOriginalEffectState original = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), null, null);
        return new(memory, original, NativeCrtRandomState.FromColdProcess());
    }

    private static NativeBattleRenzokuken160ChildStep Controller(NativeBattleRenzokuken160Frame frame, ushort counter, List<NativeBattleRenzokuken160Operation> operations)
    {
        NativeBattleRenzokukenSpecialProfile profile = NativeBattleRenzokukenSpecialProfiles.ForEffect(frame.Effect.Binding.Effect);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, 100);
        (pool, int slot) = pool.Append(profile.ControllerCallback);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, counter)) });
        return NativeBattleRenzokukenSpecialController.AdvanceLateSpecialChild(frame, pool, slot, Inputs([10, 20, 30], operations), NativeBattleRenzokukenSpecialController.ReadState(frame).Resources, _ => new(0, 0, 0, 0), Identity, null!, null, null!, 0);
    }

    private static NativeBattleRenzokuken160Inputs Inputs(ImmutableArray<byte> segments, List<NativeBattleRenzokuken160Operation> operations) => new(0, segments, _ => new(0, 0) { GeometryWorldMatrix = Identity }, (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });

    private static short Word(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
}
