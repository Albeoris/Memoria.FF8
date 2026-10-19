using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90CreatureTests
{
    [Fact]
    public void Model_projection_writes_static_argument_workspace_and_preserves_queued_arena_TIM_despite_global_alias()
    {
        var fixture = Fixture();
        byte[] tim = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(tim, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(8), 44);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(52), 14);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(56), 10);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(58), 20);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(60), 1);
        BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(64), 0xABCD);
        NativeBattleEffectArena arena = fixture.Frame.Memory.Arena!.Write(0xC000, tim);
        NativeBattleEffectTextureState textures = fixture.Frame.Memory.Textures!.EnqueueArenaTim505E30(arena, 0xC000).State;
        NativeBattleTonberry90State shared = fixture.Frame.Memory.Tonberry90!.WriteDword(0x259EFB4, 0x20DFAB8 + 0xC000);
        shared = shared with { Globals = shared.Globals.MarkUnknown(checked((int)(NativeBattleTonberry90Creature.ProjectedAddress767610 - NativeBattleTonberry90State.GlobalAddress)), 32) };
        NativeBattleTonberry90Frame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = arena, Textures = textures, Tonberry90 = shared } };
        ImmutableArray<byte> projected = Enumerable.Range(1, 16).Select(value => (byte)value).ToImmutableArray();
        NativeBattleTonberry90Frame written = NativeBattleTonberry90Creature.PublishProjected767610(frame, projected, 16);
        Assert.Same(arena, written.Memory.Arena);
        Assert.Equal(tim, written.Memory.Arena!.Read(0xC000, tim.Length));
        Assert.Equal(projected, written.Memory.Tonberry90!.Read(NativeBattleTonberry90Creature.ProjectedAddress767610, 16).ToImmutableArray());
        Assert.Throws<NotSupportedException>(() => written.Memory.Tonberry90.Read(NativeBattleTonberry90Creature.ProjectedAddress767610 + 16, 1));
        NativeBattleEffectTextureDrain drain = written.Memory.Textures!.Drain505D20(written.Memory.Arena);
        Assert.Equal(0, drain.State.Count);
        Assert.Equal(new byte[] { 0xCD, 0xAB }, drain.Uploads[^1].Bytes);
        NativeBattleTonberry90Frame restored = NativeBattleTonberry90Creature.PublishProjected767610(JsonSerializer.Deserialize<NativeBattleTonberry90Frame>(JsonSerializer.Serialize(frame))!, projected, 16);
        Assert.Equal(JsonSerializer.Serialize(written), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Creature_request_freezes_without_advancing_pose_then_restores_saved_velocity_when_accepted()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Creature.Initialize8DD0F0(fixture.Frame, fixture.Address, Resource(), 3, 0x32808080);
        NativeBattleEffectOwnedModelStep pose = NativeBattleEffectOwnedModel.Select8DD190(Read(frame, fixture.Address), frame.Memory.Tonberry90!.CreatureModelState!, Resource(), 1);
        uint director = frame.Memory.Tonberry90.DirectorAddress1547168!.Value;
        frame = frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90! with { CreatureModelState = pose.State } } };
        frame = frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90!.Write(director + 0x40, BitConverter.GetBytes((ushort)4)).Write(director + 0x44, BitConverter.GetBytes((ushort)4)) } };
        frame = Write(frame, fixture.Address, pose.Task.WriteByte(0x29, 6).WriteByte(0x26, 4).WriteDword(0x140, 0x001FFFE1).WriteDword(0x144, 0xA55AFFF1).WriteWord(0x124, 123));
        NativeBattleTonberry90CreatureServices services = new(_ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        NativeBattleTonberry90TaskStep frozen = NativeBattleTonberry90Creature.Advance7674E0(frame, fixture.Address, Resource(), services);
        Assert.Equal(1u, frozen.Frame.Memory.Tonberry90!.ReadDword(0x25A4C18));
        Assert.Equal(1u, frozen.Frame.Memory.Tonberry90.ReadDword(0x25A2118));
        Assert.Equal((ushort)0, Read(frozen.Frame, fixture.Address).ReadWord(0x136));
        Assert.Equal((ushort)0, Read(frozen.Frame, fixture.Address).ReadWord(0x124));
        frame = frozen.Frame with { Memory = frozen.Frame.Memory with { Tonberry90 = frozen.Frame.Memory.Tonberry90!.Write(director + 0x40, BitConverter.GetBytes((ushort)5)).Write(director + 0x44, BitConverter.GetBytes((ushort)5)) } };
        NativeBattleTonberry90TaskStep resumed = NativeBattleTonberry90Creature.Advance7674E0(frame, fixture.Address, Resource(), services);
        NativeBattleModulePoolTask task = Read(resumed.Frame, fixture.Address);
        Assert.Equal((byte)7, task.ReadByte(0x29));
        Assert.Equal((ushort)6, resumed.Frame.Memory.Tonberry90!.ReadWord(director + 0x44));
        Assert.Equal(0u, resumed.Frame.Memory.Tonberry90.ReadDword(0x25A2118));
        Assert.Equal(0x001FFFE1u, task.ReadDword(0x124));
        Assert.Equal(0xA55AFFF1u, task.ReadDword(0x128));
        Assert.Equal(unchecked((ushort)-1), task.ReadWord(0x4C));
        Assert.Equal((ushort)1, task.ReadWord(0x4E));
    }

    [Fact]
    public void Final_fade_publishes_anchor_stop_and_separate_body_shadow_colors_without_a_last_draw()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Creature.Initialize8DD0F0(fixture.Frame, fixture.Address, Resource(), 3, 0xAB123456);
        frame = frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90!.WriteDword(0x25A4C10, 0x12345678).WriteDword(0x25A4C14, 0xA55A4321) } };
        frame = Write(frame, fixture.Address, Read(frame, fixture.Address).WriteByte(0x29, 12).WriteWord(0x13A, 3840));
        NativeBattleTonberry90CreatureServices services = new(_ => 0x009783FF, (_, _, _, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException()) { ReadDefaultColor = _ => 0xAB123456 };
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Creature.Advance7674E0(frame, fixture.Address, Resource(), services);
        NativeBattleModulePoolTask task = Read(step.Frame, fixture.Address);
        Assert.True(step.Complete);
        Assert.Equal((byte)13, task.ReadByte(0x29));
        Assert.Equal(0x02000000u, task.ReadDword(0x58));
        Assert.Equal(0xAB000000u, task.ReadDword(0x5C));
        Assert.Equal(0x12347FFFu, step.Frame.Memory.Tonberry90!.ReadDword(0x25A4C10));
        Assert.Equal(0xA55A4321u, step.Frame.Memory.Tonberry90.ReadDword(0x25A4C14));
    }

    [Fact]
    public void Creature_phase_three_finishes_current_pose_then_selects_walk_and_integrates_new_velocity_same_tick()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Creature.Initialize8DD0F0(fixture.Frame, fixture.Address, Resource(), 3, 0x32808080);
        NativeBattleEffectOwnedModelStep pose = NativeBattleEffectOwnedModel.Select8DD190(Read(frame, fixture.Address), frame.Memory.Tonberry90!.CreatureModelState!, Resource(), 0);
        frame = frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90! with { CreatureModelState = pose.State } } };
        frame = Write(frame, fixture.Address, pose.Task.WriteByte(0x29, 3).WriteByte(0x26, 4));
        NativeBattleTonberry90CreatureServices services = new(_ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Creature.Advance7674E0(frame, fixture.Address, Resource(), services);
        NativeBattleModulePoolTask task = Read(step.Frame, fixture.Address);
        Assert.Equal((byte)4, task.ReadByte(0x29));
        Assert.Equal((byte)1, task.ReadByte(0x9C));
        Assert.Equal((ushort)0, task.ReadWord(0x136));
        Assert.Equal(unchecked((ushort)-304), task.ReadWord(0x128));
        Assert.Equal(unchecked((ushort)-19), task.ReadWord(0x50));
        Assert.Equal((ushort)1, task.ReadWord(0x24));
    }

    [Fact]
    public void Schedule_sentinel_and_zero_do_not_allocate_or_consume_random_and_signed_index_is_preserved()
    {
        var fixture = Fixture();
        NativeBattleActionPacketsState packets = NativeBattleTonberry90ControllerTests.Fixture().Inputs.Packets;
        uint read = 0;
        NativeBattleTonberry90ParticleServices services = new(packets, (_, source) =>
        {
            read = source;
            return 0x7F;
        }, (_, _) => throw new InvalidOperationException());
        var stopped = NativeBattleTonberry90Creature.Spawn7671D0(fixture.Frame, fixture.Address, 0x154EC70, -1, 32, services);
        Assert.False(stopped.Active);
        Assert.Same(fixture.Frame, stopped.Frame);
        Assert.Equal(0x154EC6Fu, read);
        var empty = NativeBattleTonberry90Creature.Spawn7671D0(fixture.Frame, fixture.Address, 0x154EC70, 0, 32, services with { ReadSchedule = (_, _) => 0 });
        Assert.True(empty.Active);
        Assert.Same(fixture.Frame, empty.Frame);
    }

    [Fact]
    public void Schedule_spawns_in_native_queue_with_two_random_draws_per_child_and_preserves_parent_links()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = Read(fixture.Frame, fixture.Address).WriteDword(0x1C, 0x00C80064).WriteDword(0x20, 0xA55A012C).WriteWord(0x78, 64);
        NativeBattleTonberry90Frame frame = Write(fixture.Frame, fixture.Address, task);
        NativeBattleTonberry90ParticleServices services = new(NativeBattleTonberry90ControllerTests.Fixture().Inputs.Packets, (_, _) => 2, (_, _) => throw new InvalidOperationException());
        var spawned = NativeBattleTonberry90Creature.Spawn7671D0(frame, fixture.Address, 0x154EC70, 0, 32, services);
        NativeBattleModulePoolTask[] children = spawned.Frame.Effect.Pools.SelectMany(pool => pool.Slots).Where(slot => slot.Callback == 0x767290).ToArray();
        Assert.Equal(2, children.Length);
        Assert.Equal((ushort)104, children[0].ReadWord(0x1C));
        Assert.Equal((ushort)366, children[0].ReadWord(0x20));
        Assert.Equal((ushort)81, children[1].ReadWord(0x1C));
        Assert.Equal((ushort)235, children[1].ReadWord(0x20));
        Assert.All(children, child =>
        {
            Assert.Equal((ushort)200, child.ReadWord(0x1E));
            Assert.Equal((ushort)0xA55A, child.ReadWord(0x22));
            Assert.Equal(fixture.Address, child.ReadDword(0x18));
        });
        Assert.Equal((byte)2, Read(spawned.Frame, fixture.Address).ReadByte(0x28));
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 4; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, spawned.Frame.Random);
        var restored = NativeBattleTonberry90Creature.Spawn7671D0(JsonSerializer.Deserialize<NativeBattleTonberry90Frame>(JsonSerializer.Serialize(frame))!, fixture.Address, 0x154EC70, 0, 32, services);
        Assert.Equal(JsonSerializer.Serialize(spawned), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Moving_sprite_damps_signed_words_before_integration_and_hides_only_after_frame_limit()
    {
        var fixture = NativeBattleTonberry90ControllerTests.Fixture();
        int pool = Enumerable.Range(0, fixture.Frame.Effect.Pools.Length).Single(index => fixture.Frame.Effect.Pools[index].DescriptorAddress == 0x259F098);
        Assert.Equal(0x7C, fixture.Frame.Effect.Pools[pool].Stride);
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(pool, 0x767C70, 0x7C, NativeBattleTonberry90Controller.MainBank, (_, _, _) => new(0, 3));
        NativeBattleTonberry90Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = appended.Hierarchy.Pools } };
        NativeBattleModulePoolTask task = Read(frame, appended.Address).WriteByte(0x29, 1).WriteWord(0x50, 9).WriteWord(0x52, 9).WriteWord(0x6C, unchecked((ushort)-17)).WriteWord(0x6E, unchecked((ushort)-97)).WriteWord(0x70, 32).WriteWord(0x1C, 10).WriteWord(0x20, 20);
        frame = Write(frame, appended.Address, task);
        int draws = 0;
        NativeBattleTonberry90ParticleServices services = new(fixture.Inputs.Packets, (_, _) => throw new InvalidOperationException(), (current, address) =>
        {
            Assert.Equal((byte)5, (byte)(Read(current, address).ReadByte(0x26) & 5));
            draws++;
            return current;
        });
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Creature.Advance767290(frame, appended.Address, services);
        task = Read(step.Frame, appended.Address);
        Assert.Equal(1, draws);
        Assert.True(step.Complete);
        Assert.Equal(unchecked((ushort)-15), task.ReadWord(0x6C));
        Assert.Equal(unchecked((ushort)-1), task.ReadWord(0x6E));
        Assert.Equal((ushort)28, task.ReadWord(0x70));
        Assert.Equal((ushort)10, task.ReadWord(0x1C));
        Assert.Equal((ushort)21, task.ReadWord(0x20));
        Assert.Equal((ushort)9, task.ReadWord(0x50));
    }

    [Fact]
    public void ConstructorPublishesOnlyActualSparseFieldsAndInitialStateSelectsRealPoseBeforeSound()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Creature.Initialize8DD0F0(fixture.Frame, fixture.Address, Resource(), 3, 0x32123456);
        NativeBattleModulePoolTask task = Read(frame, fixture.Address);
        Assert.Equal(fixture.Address + 0xCC, task.ReadDword(0x94));
        Assert.Equal(0x32123456u, task.ReadDword(0x5C));
        Assert.Equal(0x00808080u, task.ReadDword(0x58));
        Assert.Equal((ushort)3, task.ReadWord(0xDE));
        Assert.Null(frame.Memory.Tonberry90!.CreatureModelState!.DecodedPose);
        uint director = frame.Memory.Tonberry90.DirectorAddress1547168!.Value;
        frame = frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90.WriteDword(director, 0xFFFE1234).WriteDword(director + 4, 0xA55AFFFD) } };
        List<uint> sounds = [];
        NativeBattleTonberry90CreatureServices services = new(_ => throw new InvalidOperationException("Fade is not reached."), (current, descriptor, pan, volume) =>
        {
            Assert.NotNull(current.Memory.Tonberry90!.CreatureModelState!.DecodedPose);
            Assert.Equal(128, pan);
            Assert.Equal(127u, volume);
            sounds.Add(descriptor);
            return current;
        }, (_, _, _, _) => throw new InvalidOperationException("Initial creature is hidden."));
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Creature.Advance7674E0(frame, fixture.Address, Resource(), services);
        task = Read(step.Frame, fixture.Address);
        Assert.Equal(new uint[] { 0x154716C, 0x16D7DE8 }, sounds);
        Assert.Equal(0xFFFE1234u, task.ReadDword(0x4C));
        Assert.Equal(0xA55AFFFDu, task.ReadDword(0x50));
        Assert.Equal(fixture.Address + 0x114, task.ReadDword(0x60));
        Assert.Equal(4096u, task.ReadDword(0x118));
        Assert.Equal((byte)3, task.ReadByte(0x9C));
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((ushort)1, task.ReadWord(0x24));
        Assert.Equal((byte)4, (byte)(task.ReadByte(0x26) & 4));
    }

    [Fact]
    public void RecolorPreservesUnwrittenAlphaAndCodeExceptExactModeStoresAndUsesTruncation()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0, new byte[0x148 - 12].ToImmutableArray());
        task = task.WriteByte(0x37, 0xA5).WriteDword(0x58, 0x7F123456);
        NativeBattleModulePoolTask half = NativeBattleTonberry90Creature.Recolor7678D0(task, 2048, 1, 0x009783FF);
        Assert.Equal(0x02262140u, half.ReadDword(0x58));
        Assert.Equal((byte)0xA5, half.ReadByte(0x37));
        NativeBattleModulePoolTask unchanged = NativeBattleTonberry90Creature.Recolor7678D0(task, -1, 0, 0x009783FF);
        Assert.Equal(0x7F9783FFu, unchanged.ReadDword(0x58));
        NativeBattleModulePoolTask alpha = NativeBattleTonberry90Creature.Recolor7678D0(task, 4096, 3, 0x009783FF);
        Assert.Equal(0x02000000u, alpha.ReadDword(0x58));
        Assert.Equal((byte)0, alpha.ReadByte(0x37));
        NativeBattleModulePoolTask reset = NativeBattleTonberry90Creature.Recolor7678D0(task, 0, 3, 0xAB9783FF);
        Assert.Equal(0x009783FFu, reset.ReadDword(0x58));
        Assert.Equal((byte)0, reset.ReadByte(0x37));
    }

    [Fact]
    public void LiteralRetStillIntegratesSignedTruncatedVelocityAndWrapsClockBeforeCompletion()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Creature.Initialize8DD0F0(fixture.Frame, fixture.Address, Resource(), 3, 0x32808080);
        NativeBattleModulePoolTask task = Read(frame, fixture.Address).WriteByte(0x29, 13).WriteByte(0x26, 5).WriteWord(0x24, 0xFFFF).WriteWord(0x4E, 0xFFFF).WriteWord(0x124, unchecked((ushort)-31)).WriteWord(0x126, 31).WriteWord(0x128, unchecked((ushort)-15));
        frame = Write(frame, fixture.Address, task);
        NativeBattleTonberry90CreatureServices services = new(_ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        NativeBattleTonberry90TaskStep first = NativeBattleTonberry90Creature.Advance7674E0(frame, fixture.Address, Resource(), services);
        NativeBattleTonberry90TaskStep second = NativeBattleTonberry90Creature.Advance7674E0(JsonSerializer.Deserialize<NativeBattleTonberry90Frame>(JsonSerializer.Serialize(frame))!, fixture.Address, Resource(), services);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.True(first.Complete);
        task = Read(first.Frame, fixture.Address);
        Assert.Equal((ushort)0xFFFF, task.ReadWord(0x4C));
        Assert.Equal((ushort)0, task.ReadWord(0x4E));
        Assert.Equal((ushort)0, task.ReadWord(0x50));
        Assert.Equal((ushort)0, task.ReadWord(0x24));
    }

    private static (NativeBattleTonberry90Frame Frame, uint Address) Fixture()
    {
        var fixture = NativeBattleTonberry90ControllerTests.Fixture();
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(5, 0x7674E0, 0x148, NativeBattleTonberry90Controller.MainBank, (_, _, _) => new(0, 3));
        return (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    private static NativeBattleEffectOwnedModelResource Resource()
    {
        byte[] data = new byte[140];
        BinaryPrimitives.WriteInt32LittleEndian(data, 4);
        int[] offsets = [24, 88, 96, 132, 140];
        for (int index = 0; index < offsets.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4 + index * 4), offsets[index]);
        }
        data[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(40), -1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(96), 4);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(100 + index * 4), 20 + index * 4);
            data[116 + index * 4] = 1;
        }
        NativeBattlePoseSource pose = new(data.AsSpan(24, 64).ToArray().ToImmutableArray(), data.AsSpan(96, 36).ToArray().ToImmutableArray());
        return new(0x1000, data.ToImmutableArray(), pose, []);
    }
    private static NativeBattleModulePoolTask Read(NativeBattleTonberry90Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleTonberry90Frame Write(NativeBattleTonberry90Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
