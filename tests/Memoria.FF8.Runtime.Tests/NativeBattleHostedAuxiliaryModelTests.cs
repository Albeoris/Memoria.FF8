using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedAuxiliaryModelTests
{
    private static readonly BattlePartyActorModel NativeParty = new(new("native"));
    private static readonly BattlePartyActorModel AddedAlly = new(new("extra-ally"));
    private static readonly BattleEnemyActorModel AddedEnemy = new(new("extra-enemy"));
    private static readonly BattleEnemyActorModel Missing = new(new("missing"));
    private static readonly BattleModelRoster Roster = new([NativeParty, AddedAlly, AddedEnemy]);
    private static readonly NativeBattleModelAliases Aliases = new(Roster, [(NativeBattleModelSlot.Decode(0), NativeParty)]);

    [Fact]
    public void Hosted_ring_texture_and_sound_tasks_keep_high_rows_across_inflight_restore()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelStatus50A000, AddedAlly, Aliases, flags: 0xABCDEF).State;
        state = state.AppendTextureAnimation(AddedEnemy, 9, new([2, 255], 0), Aliases);
        NativeGlobalRandomState initial = new(5);
        (state, NativeGlobalRandomState random, bool allocated) = state.AppendSoundParticle(AddedAlly, false, 240, new([2, 255], 0), initial, Aliases);
        Assert.True(allocated);
        Assert.Equal(initial.Draw(out _), random);
        Assert.Equal(AddedAlly, state.Slots[0].ModelOwner);
        Assert.Equal(AddedEnemy, state.Slots[1].ModelOwner);
        Assert.Equal(AddedAlly, state.Slots[2].ModelOwner);
        Assert.Equal(0xFu, state.Slots[0].UnknownByteMask & 0xFu);
        Assert.Equal(0xFu, state.Slots[1].UnknownByteMask & 0xFu);
        Assert.Equal(0xF0000u, state.Slots[2].UnknownByteMask & 0xF0000u);

        List<(NativeBattleAuxiliaryKind Kind, BattleModelOwner Owner, uint Value)> rings = [];
        List<(BattleModelOwner Owner, byte Slot, byte Image)> uploads = [];
        NativeBattleGraphicsModel model = new(2) { ResourceFlags44 = 0, Sequence = 9 };
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ApplyModelRing = (kind, row, value) => rings.Add((kind, row, value)),
            ApplyTexture = (row, slot, image) => uploads.Add((row, slot, image)),
            ReadModel = row => row == AddedEnemy ? model : throw new InvalidOperationException(),
            ReadBone = (row, bone, fraction) =>
            {
                Assert.Equal(((BattleModelOwner)AddedAlly, (byte)240, (short)0), (row, bone, fraction));
                return new(8, 12, 16);
            },
            ReadPan = anchor =>
            {
                Assert.Equal(new NativeBattleVector(8, 0, 16), anchor);
                return 22;
            }
        };
        NativeBattleAuxiliaryStep first = state.Advance(inputs);
        Assert.Single(rings);
        Assert.Equal((NativeBattleAuxiliaryKind.ModelStatus50A000, (BattleModelOwner)AddedAlly, 0xABCDEFu), rings[0]);
        Assert.Empty(uploads);
        Assert.Empty(first.Emissions);

        NativeBattleAuxiliaryState restored = NativeBattleTestSnapshots.Independent(first.State);
        NativeBattleAuxiliaryStep second = restored.Advance(inputs);
        Assert.Equal(((BattleModelOwner)AddedEnemy, (byte)0, (byte)1), Assert.Single(uploads));
        NativeBattleAuxiliaryEmission emission = Assert.Single(second.Emissions);
        Assert.Equal(AddedAlly, emission.Model);
        Assert.Equal((ushort)3, emission.Sound);
        Assert.Equal((byte)22, emission.Pan);
        Assert.True(emission.Particle);
    }

    [Fact]
    public void Hosted_source_and_target_motion_preserve_direction_and_resume()
    {
        var start = new NativeBattleVector(0, 3, 0);
        var finish = new NativeBattleVector(100, 6, 200);
        NativeBattleGraphicsModel mover = new(2) { Transform = new(start, start, 0, 0, 4096) };
        NativeBattleGraphicsModel target = new(2) { Transform = new(finish, finish, 0, 0, 4096), Flags2 = 0x20, GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() };
        List<BattleModelOwner> writes = [];
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ReadModel = row => row == AddedAlly ? mover : row == AddedEnemy ? target : throw new InvalidOperationException(),
            WriteModel = (row, model) =>
            {
                writes.Add(row);
                if (row == AddedAlly)
                {
                    mover = model;
                }
                else
                {
                    Assert.Equal(AddedEnemy, row);
                    target = model;
                }
            },
            ReadBone = (row, bone, fraction) =>
            {
                Assert.Equal(((BattleModelOwner)AddedEnemy, (byte)242, (short)2048), (row, bone, fraction));
                return new(90, 4, 180);
            }
        };

        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendModelMotion(0x9E, AddedAlly, AddedEnemy, 0, 0, 0, 2, Aliases).State;
        state = state.Advance(inputs).State;
        Assert.Equal(new NativeBattleVector(50, 3, 100), mover.Transform!.Position28);
        state = NativeBattleTestSnapshots.Independent(state).Advance(inputs).State;
        Assert.Null(state.Head);
        Assert.Equal(new NativeBattleVector(100, 3, 200), mover.Transform!.Initial20);

        state = state.AppendModelMotion(0xAD, AddedAlly, AddedEnemy, 255, 255, 0, 1, Aliases).State.Advance(inputs).State;
        Assert.Null(state.Head);
        Assert.Equal(new BattleModelOwner[] { AddedAlly, AddedAlly, AddedEnemy }, writes);
        Assert.Equal((10, 2, 20), (target.GeometryWorldMatrix!.X, target.GeometryWorldMatrix.Y, target.GeometryWorldMatrix.Z));
        Assert.Equal((ushort)0, target.Flags2);
    }

    [Fact]
    public void Unowned_model_rows_are_rejected_without_model_callbacks_or_slot_publication()
    {
        NativeBattleAuxiliaryState empty = NativeBattleAuxiliaryState.Clear();
        NativeBattleAuxiliaryProgram program = new([1, 255], 0);
        NativeGlobalRandomState random = new(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, Missing, Aliases));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendModelMotion(0x9E, AddedAlly, Missing, 0, 0, 0, 2, Aliases));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendTextureAnimation(Missing, 4, program, Aliases));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendSoundParticle(Missing, true, 0, program, random, Aliases));
        Assert.Null(empty.Head);
        Assert.Equal(random.Draw(out _), empty.AppendSoundParticle(AddedEnemy, true, 0, program, random, Aliases).Random);

        NativeBattleAuxiliaryInputs inputs = new()
        {
            ApplyModelRing = (_, _, _) => throw new InvalidOperationException(),
            ReadModel = _ => throw new InvalidOperationException(),
            WriteModel = (_, _) => throw new InvalidOperationException(),
            ApplyTexture = (_, _, _) => throw new InvalidOperationException(),
            ReadBone = (_, _, _) => throw new InvalidOperationException()
        };
        NativeBattleAuxiliaryState ring = empty.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, AddedAlly, Aliases).State;
        NativeBattleAuxiliaryState motion = empty.AppendModelMotion(0x9E, AddedAlly, AddedEnemy, 0, 0, 0, 2, Aliases).State;
        NativeBattleAuxiliaryState texture = empty.AppendTextureAnimation(AddedEnemy, 4, program, Aliases);
        NativeBattleAuxiliaryState sound = empty.AppendSoundParticle(AddedEnemy, true, 0, program, random, Aliases).State;
        Assert.Throws<InvalidDataException>(() => RemoveOwner(ring).Advance(inputs));
        Assert.Throws<InvalidDataException>(() => RemoveOwner(motion, target: true).Advance(inputs));
        Assert.Throws<InvalidDataException>(() => RemoveOwner(texture).Advance(inputs));
        Assert.Throws<InvalidDataException>(() => RemoveOwner(sound).Advance(inputs));
    }

    [Fact]
    public void Native_rows_keep_their_packet_bytes_and_full_slots_do_not_consume_sound_random()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        var native = state.AppendModelMotion(0xAE, OriginalOwner(2), OriginalOwner(6), 2, 3, -1024, 4, OriginalAliases);
        Assert.Equal(0x1D972C0u + 2 * 156, BinaryPrimitives.ReadUInt32LittleEndian(native.State.Slots[0].Payload.AsSpan()));
        for (int slot = 0; slot < 16; slot++)
        {
            state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, NativeParty, Aliases).State;
        }
        NativeGlobalRandomState random = new(5);
        var full = state.AppendSoundParticle(NativeParty, true, 0, new([1, 255], 0), random, Aliases);
        Assert.False(full.Allocated);
        Assert.Equal(random, full.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(full.State));
    }

    [Fact]
    public void Hosted_group_fade_resumes_its_frame_after_detached_restore()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendModelGroupFade50F0B0(AddedEnemy, 2, Aliases);
        Assert.Equal(AddedEnemy, state.Slots[0].ModelOwner);
        Assert.Equal(0xFu, state.Slots[0].UnknownByteMask & 0xFu);
        NativeBattleGraphicsModel model = new(2) { SequenceState = new() { MeshMask124 = 0xF } };
        List<byte> frames = [];
        int mutations = 0;
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ReadModel = row => row == AddedEnemy ? model : throw new InvalidOperationException(),
            WriteModel = (row, value) =>
            {
                Assert.Equal(AddedEnemy, row);
                mutations++;
                model = value;
            },
            DrawModelGroup50F190 = (row, group, frame, color) =>
            {
                Assert.Equal(((BattleModelOwner)AddedEnemy, (byte)2, 255u), (row, group, color));
                frames.Add(frame);
            }
        };
        state = state.Advance(inputs).State;
        Assert.Equal(0xBu, model.SequenceState!.MeshMask124);
        state = NativeBattleTestSnapshots.Independent(state);
        for (int frame = 1; frame < 15; frame++)
        {
            state = state.Advance(inputs).State;
        }
        Assert.Null(state.Head);
        Assert.Equal(1, mutations);
        Assert.Equal(Enumerable.Range(0, 15).Select(frame => (byte)frame), frames);
        Assert.Throws<InvalidDataException>(() => RemoveOwner(NativeBattleAuxiliaryState.Clear().AppendModelGroupFade50F0B0(AddedEnemy, 2, Aliases)).Advance(inputs));
    }

    [Fact]
    public void Deformation_retains_task_bytes_and_rejects_unowned_rows_before_using_model_state()
    {
        NativeBattleAuxiliaryState empty = NativeBattleAuxiliaryState.Clear();
        NativeBattleVector velocity = new(11, -22, 33);
        NativeBattleAuxiliaryState state = empty.AppendModelDeformation50F250(AddedAlly, 2, 5, velocity, -6, 0xABCDEF, Aliases);
        NativeBattleAuxiliaryTask task = state.Slots[0];
        Assert.Equal(NativeBattleAuxiliaryKind.ModelDeformation50F2E0, task.Kind);
        Assert.Equal(AddedAlly, task.ModelOwner);
        Assert.Equal(0xFu, task.UnknownByteMask & 0xFu);
        Assert.Equal(new byte[] { 2, 5, 0 }, task.Payload.AsSpan().Slice(4, 3).ToArray());
        Assert.Equal(velocity.X, BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(8)));
        Assert.Equal(velocity.Y, BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(10)));
        Assert.Equal(velocity.Z, BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(12)));
        Assert.Equal(0xABCDEFu, BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(16)));
        Assert.Equal((short)-6, BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(20)));
        Assert.Equal(0xC00Fu, task.UnknownByteMask);
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendModelDeformation50F250(Missing, 0, 0, default, 0, 0, Aliases));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendModelGroupFade50F0B0(Missing, 0, Aliases));

        NativeBattleAuxiliaryState native = empty.AppendModelDeformation50F250(NativeParty, 2, 5, velocity, -6, 0xABCDEF, Aliases);
        Assert.Equal(0x1D972C0u, BinaryPrimitives.ReadUInt32LittleEndian(native.Slots[0].Payload.AsSpan()));
        Assert.Equal(0u, native.Slots[0].UnknownByteMask & 0xFu);
        NativeBattleAuxiliaryState full = empty;
        for (int slot = 0; slot < 16; slot++)
        {
            full = full.AppendModelGroupFade50F0B0(NativeParty, 2, Aliases);
        }
        Assert.Same(full, full.AppendModelDeformation50F250(AddedAlly, 2, 5, velocity, -6, 0, Aliases));
        Assert.Throws<InvalidOperationException>(() => full.AppendModelGroupFade50F0B0(AddedAlly, 2, Aliases));

        BattleSession battle = new(new()
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        NativeBattleHostedTestFixture.BindProcess(battle);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(Roster, Aliases, _ => new(0));
        MethodInfo advance = typeof(BattlePresentationProcess).GetMethod("AdvanceNativeModelDeformation50F2E0", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object?[] arguments = [graphics, RemoveOwner(state).Slots[0]];
        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => advance.Invoke(battle.Presentation!, arguments));
        Assert.IsType<InvalidDataException>(invocation.InnerException);
        Assert.Same(graphics, arguments[0]);
        Assert.Null(battle.Presentation?.Graphics);
    }

    private static NativeBattleAuxiliaryState RemoveOwner(NativeBattleAuxiliaryState state, bool target = false)
    {
        NativeBattleAuxiliaryTask task = state.Slots[0];
        task = target ? task with { TargetOwner = null } : task with { ModelOwner = null };
        return state with { Slots = state.Slots.SetItem(0, task) };
    }
}
