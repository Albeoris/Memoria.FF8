using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedAuxiliaryModelTests
{
    [Fact]
    public void Hosted_ring_texture_and_sound_tasks_keep_high_rows_across_inflight_restore()
    {
        BattleModelRoster roster = Roster();
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelStatus50A000, 7, 0xABCDEF, roster: roster).State;
        state = state.AppendTextureAnimation(24, 9, new([2, 255], 0), roster);
        NativeGlobalRandomState initial = new(5);
        (state, NativeGlobalRandomState random, bool allocated) = state.AppendSoundParticle(7, false, 240, new([2, 255], 0), initial, roster);
        Assert.True(allocated);
        Assert.Equal(initial.Draw(out _), random);
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(7), BinaryPrimitives.ReadUInt32LittleEndian(state.Slots[0].Payload.AsSpan()));
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(24), BinaryPrimitives.ReadUInt32LittleEndian(state.Slots[1].Payload.AsSpan()));
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(7), BinaryPrimitives.ReadUInt32LittleEndian(state.Slots[2].Payload.AsSpan().Slice(16)));

        List<(NativeBattleAuxiliaryKind Kind, int Row, uint Value)> rings = [];
        List<(int Row, byte Slot, byte Image)> uploads = [];
        NativeBattleGraphicsModel model = new(24, 2) { ResourceFlags44 = 0, Sequence = 9 };
        NativeBattleAuxiliaryInputs inputs = new()
        {
            PresentationRoster = roster,
            ApplyModelRing = (kind, row, value) => rings.Add((kind, row, value)),
            ApplyTexture = (row, slot, image) => uploads.Add((row, slot, image)),
            ReadModel = row => row == 24 ? model : throw new InvalidOperationException(),
            ReadBone = (row, bone, fraction) =>
            {
                Assert.Equal((7, (byte)240, (short)0), (row, bone, fraction));
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
        Assert.Equal((NativeBattleAuxiliaryKind.ModelStatus50A000, 7, 0xABCDEFu), rings[0]);
        Assert.Empty(uploads);
        Assert.Empty(first.Emissions);

        NativeBattleAuxiliaryState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAuxiliaryState>(NativeBattleTestSnapshots.Json(first.State));
        NativeBattleAuxiliaryStep second = restored.Advance(inputs);
        Assert.Equal((24, (byte)0, (byte)1), Assert.Single(uploads));
        NativeBattleAuxiliaryEmission emission = Assert.Single(second.Emissions);
        Assert.Equal(7, emission.Model);
        Assert.Equal((ushort)3, emission.Sound);
        Assert.Equal((byte)22, emission.Pan);
        Assert.True(emission.Particle);
    }

    [Fact]
    public void Hosted_source_and_target_motion_preserve_direction_and_resume()
    {
        BattleModelRoster roster = Roster();
        var start = new NativeBattleVector(0, 3, 0);
        var finish = new NativeBattleVector(100, 6, 200);
        NativeBattleGraphicsModel mover = new(7, 2) { Transform = new(start, start, 0, 0, 4096) };
        NativeBattleGraphicsModel target = new(24, 2) { Transform = new(finish, finish, 0, 0, 4096), Flags2 = 0x20, GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() };
        List<int> writes = [];
        NativeBattleAuxiliaryInputs inputs = new()
        {
            PresentationRoster = roster,
            ReadModel = row => row == 7 ? mover : row == 24 ? target : throw new InvalidOperationException(),
            WriteModel = (row, model) =>
            {
                writes.Add(row);
                if (row == 7)
                {
                    mover = model;
                }
                else
                {
                    Assert.Equal(24, row);
                    target = model;
                }
            },
            ReadBone = (row, bone, fraction) =>
            {
                Assert.Equal((24, (byte)242, (short)2048), (row, bone, fraction));
                return new(90, 4, 180);
            }
        };

        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendModelMotion(0x9E, 7, 24, 0, 0, 0, 2, roster).State;
        state = state.Advance(inputs).State;
        Assert.Equal(new NativeBattleVector(50, 3, 100), mover.Transform!.Position28);
        state = NativeBattleTestSnapshots.RestoreJson<NativeBattleAuxiliaryState>(NativeBattleTestSnapshots.Json(state)).Advance(inputs).State;
        Assert.Null(state.Head);
        Assert.Equal(new NativeBattleVector(100, 3, 200), mover.Transform!.Initial20);

        state = state.AppendModelMotion(0xAD, 7, 24, 255, 255, 0, 1, roster).State.Advance(inputs).State;
        Assert.Null(state.Head);
        Assert.Equal(new[] { 7, 7, 24 }, writes);
        Assert.Equal((10, 2, 20), (target.GeometryWorldMatrix!.X, target.GeometryWorldMatrix.Y, target.GeometryWorldMatrix.Z));
        Assert.Equal((ushort)0, target.Flags2);
    }

    [Fact]
    public void Unowned_model_rows_are_rejected_without_model_callbacks_or_slot_publication()
    {
        BattleModelRoster roster = Roster();
        NativeBattleAuxiliaryState empty = NativeBattleAuxiliaryState.Clear();
        NativeBattleAuxiliaryProgram program = new([1, 255], 0);
        NativeGlobalRandomState random = new(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, 1, roster: roster));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendModelMotion(0x9E, 7, 25, 0, 0, 0, 2, roster));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendTextureAnimation(1, 4, program, roster));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendSoundParticle(1, true, 0, program, random, roster));
        Assert.Null(empty.Head);
        Assert.Equal(random.Draw(out _), empty.AppendSoundParticle(24, true, 0, program, random, roster).Random);

        NativeBattleAuxiliaryInputs inputs = new()
        {
            PresentationRoster = roster,
            ApplyModelRing = (_, _, _) => throw new InvalidOperationException(),
            ReadModel = _ => throw new InvalidOperationException(),
            WriteModel = (_, _) => throw new InvalidOperationException(),
            ApplyTexture = (_, _, _) => throw new InvalidOperationException(),
            ReadBone = (_, _, _) => throw new InvalidOperationException()
        };
        NativeBattleAuxiliaryState ring = empty.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, 7, roster: roster).State;
        NativeBattleAuxiliaryState motion = empty.AppendModelMotion(0x9E, 7, 24, 0, 0, 0, 2, roster).State;
        NativeBattleAuxiliaryState texture = empty.AppendTextureAnimation(24, 4, program, roster);
        NativeBattleAuxiliaryState sound = empty.AppendSoundParticle(24, true, 0, program, random, roster).State;
        Assert.Throws<InvalidDataException>(() => ReplacePointer(ring, 0, NativeBattleGraphicsModelAddress.Encode(1)).Advance(inputs));
        Assert.Throws<NotSupportedException>(() => ReplacePointer(motion, 4, NativeBattleGraphicsModelAddress.Encode(1)).Advance(inputs));
        Assert.Throws<InvalidDataException>(() => ReplacePointer(texture, 0, NativeBattleGraphicsModelAddress.Encode(1)).Advance(inputs));
        Assert.Throws<InvalidDataException>(() => ReplacePointer(sound, 16, uint.MaxValue).Advance(inputs));
    }

    [Fact]
    public void Native_rows_keep_their_packet_bytes_and_full_slots_do_not_consume_sound_random()
    {
        BattleModelRoster roster = new(Enumerable.Range(0, 7).Select(row => (BattleModelOwner?)new BattleModelOwner($"native-{row}", row < 3)).ToImmutableArray());
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        var native = state.AppendModelMotion(0xAE, 2, 6, 2, 3, -1024, 4);
        var explicitRoster = state.AppendModelMotion(0xAE, 2, 6, 2, 3, -1024, 4, roster);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.State), NativeBattleTestSnapshots.Json(explicitRoster.State));
        Assert.Equal(0x1D972C0u + 2 * 156, BinaryPrimitives.ReadUInt32LittleEndian(native.State.Slots[0].Payload.AsSpan()));
        for (int slot = 0; slot < 16; slot++)
        {
            state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, 0).State;
        }
        NativeGlobalRandomState random = new(5);
        var full = state.AppendSoundParticle(0, true, 0, new([1, 255], 0), random);
        Assert.False(full.Allocated);
        Assert.Equal(random, full.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(full.State));
    }

    [Fact]
    public void Hosted_group_fade_resumes_its_frame_after_detached_restore()
    {
        BattleModelRoster roster = Roster();
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendModelGroupFade50F0B0(24, 2, roster);
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(24), BinaryPrimitives.ReadUInt32LittleEndian(state.Slots[0].Payload.AsSpan()));
        NativeBattleGraphicsModel model = new(24, 2) { SequenceState = new() { MeshMask124 = 0xF } };
        List<byte> frames = [];
        int mutations = 0;
        NativeBattleAuxiliaryInputs inputs = new()
        {
            PresentationRoster = roster,
            ReadModel = row => row == 24 ? model : throw new InvalidOperationException(),
            WriteModel = (row, value) =>
            {
                Assert.Equal(24, row);
                mutations++;
                model = value;
            },
            DrawModelGroup50F190 = (row, group, frame, color) =>
            {
                Assert.Equal((24, (byte)2, 255u), (row, group, color));
                frames.Add(frame);
            }
        };
        state = state.Advance(inputs).State;
        Assert.Equal(0xBu, model.SequenceState!.MeshMask124);
        state = NativeBattleTestSnapshots.RestoreJson<NativeBattleAuxiliaryState>(NativeBattleTestSnapshots.Json(state));
        for (int frame = 1; frame < 15; frame++)
        {
            state = state.Advance(inputs).State;
        }
        Assert.Null(state.Head);
        Assert.Equal(1, mutations);
        Assert.Equal(Enumerable.Range(0, 15).Select(frame => (byte)frame), frames);
        Assert.Throws<NotSupportedException>(() => ReplacePointer(NativeBattleAuxiliaryState.Clear().AppendModelGroupFade50F0B0(24, 2, roster), 0, NativeBattleGraphicsModelAddress.Encode(1)).Advance(inputs));
    }

    [Fact]
    public void Deformation_retains_task_bytes_and_rejects_unowned_rows_before_using_model_state()
    {
        BattleModelRoster roster = Roster();
        NativeBattleAuxiliaryState empty = NativeBattleAuxiliaryState.Clear();
        NativeBattleVector velocity = new(11, -22, 33);
        NativeBattleAuxiliaryState state = empty.AppendModelDeformation50F250(7, 2, 5, velocity, -6, 0xABCDEF, roster);
        NativeBattleAuxiliaryTask task = state.Slots[0];
        Assert.Equal(NativeBattleAuxiliaryKind.ModelDeformation50F2E0, task.Kind);
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(7), BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan()));
        Assert.Equal(new byte[] { 2, 5, 0 }, task.Payload.AsSpan().Slice(4, 3).ToArray());
        Assert.Equal(velocity.X, BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(8)));
        Assert.Equal(velocity.Y, BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(10)));
        Assert.Equal(velocity.Z, BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(12)));
        Assert.Equal(0xABCDEFu, BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(16)));
        Assert.Equal((short)-6, BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(20)));
        Assert.Equal(0xC000u, task.UnknownByteMask);
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendModelDeformation50F250(1, 0, 0, default, 0, 0, roster));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.AppendModelGroupFade50F0B0(25, 0, roster));

        var native = empty.AppendModelDeformation50F250(0, 2, 5, velocity, -6, 0xABCDEF);
        var bound = empty.AppendModelDeformation50F250(0, 2, 5, velocity, -6, 0xABCDEF, roster);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(bound));
        NativeBattleAuxiliaryState full = empty;
        for (int slot = 0; slot < 16; slot++)
        {
            full = full.AppendModelGroupFade50F0B0(0, 2);
        }
        Assert.Same(full, full.AppendModelDeformation50F250(7, 2, 5, velocity, -6, 0));
        Assert.Same(full, full.AppendModelDeformation50F250(7, 2, 5, velocity, -6, 0, roster));
        Assert.Throws<InvalidOperationException>(() => full.AppendModelGroupFade50F0B0(7, 2, roster));

        BattleSession battle = new(new()
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        MethodInfo advance = typeof(BattleSession).GetMethod("AdvanceNativeModelDeformation50F2E0", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (uint pointer in new[] { NativeBattleGraphicsModelAddress.Encode(1), uint.MaxValue })
        {
            object?[] args = [graphics, ReplacePointer(state, 0, pointer).Slots[0]];
            InvalidDataException error = Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => advance.Invoke(battle, args)).InnerException);
            Assert.Contains("bound row", error.Message);
        }
        Assert.Null(battle.NativeGraphics);
    }

    private static BattleModelRoster Roster()
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[0] = new("native", true);
        rows[7] = new("extra-ally", true);
        rows[24] = new("extra-enemy", false);
        return new(rows.ToImmutableArray());
    }

    private static NativeBattleAuxiliaryState ReplacePointer(NativeBattleAuxiliaryState state, int offset, uint address)
    {
        NativeBattleAuxiliaryTask task = state.Slots[0];
        byte[] payload = task.Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset), address);
        return state with { Slots = state.Slots.SetItem(0, task with { Payload = payload.ToImmutableArray() }) };
    }
}
