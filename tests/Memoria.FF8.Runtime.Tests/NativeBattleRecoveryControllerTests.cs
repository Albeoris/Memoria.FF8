using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRecoveryControllerTests
{
    [Theory]
    [InlineData((ushort)53, 0x6000, 35, 100, 10, 10, 0, 30)]
    [InlineData((ushort)58, 0x9000, 45, 80, 8, 12, 0, 22)]
    [InlineData((ushort)59, 0x9000, 45, 80, 18, 18, 8, 12)]
    public void Entry_keeps_original_capacities_current_initializer_pointer_and_profile_timing(ushort effect, int bank, int emitters, int geometry, int release, int contact, int sound, int wait)
    {
        var fixture = Fixture(effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        Assert.Equal(new[] { 2, 4, 4, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(bank, profile.DrawBankBytes);
        Assert.Equal(emitters * 108, profile.EmitterBytes);
        Assert.Equal(geometry * 672, profile.GeometryBytes);
        Assert.Equal(release, profile.TargetReleaseCounter);
        Assert.Equal(contact, profile.ContactCounter);
        Assert.Equal(sound, profile.SoundCounter);
        Assert.Equal(wait, profile.RootWait);
        Assert.Equal(256 + bank * 2, fixture.Frame.State.AuxiliaryCursor);
        Assert.Throws<NotSupportedException>(() => NativeBattleRecoveryController.Initialize(fixture.Frame.Memory, fixture.Frame.Effect with { ResourcePointers = [] }, fixture.Inputs.Packets, Resources(effect)));
        Assert.Null(NativeBattleRecoveryController.Initialize(fixture.Frame.Memory with { Textures = null }, fixture.Frame.Effect, fixture.Inputs.Packets, Resources(effect)).Memory.Textures);
    }

    [Theory]
    [InlineData((ushort)53)]
    [InlineData((ushort)58)]
    [InlineData((ushort)59)]
    public void Original_clear_precedes_same_pass_leaf_and_context_uses_absolute_directory(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        NativeBattleRecoveryFrame frame = fixture.Frame;
        List<uint> callbacks = [];
        for (int callback = 0; callback < 4; callback++)
        {
            frame = NativeBattleRecoveryController.Advance(frame, fixture.Inputs, Resources(effect), (current, address) =>
            {
                callbacks.Add(new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
                Assert.All(current.Memory.Arena!.Read(current.State.Emitters!.Value, profile.EmitterBytes + profile.GeometryBytes), value => Assert.Equal((byte)0, value));
                return new(current, 0);
            }).Frame;
        }
        Assert.Equal(new[] { profile.FadeInCallback, profile.GenericCallback, profile.FadeInCallback }, callbacks);
        NativeBattleModulePoolTask task = Assert.Single(frame.Effect.Pools[2].Slots, slot => slot.Callback == profile.GenericCallback);
        Assert.Equal(profile.DirectoryAddress, task.ReadDword(0x30));
        Assert.Equal((ushort)45, task.ReadWord(0x29E));
        Assert.Equal((ushort)0, frame.State.EmitterScanCursor);
        Assert.Equal((ushort)0, frame.State.GeometryScanCursor);
    }

    [Theory]
    [InlineData((ushort)53)]
    [InlineData((ushort)58)]
    [InlineData((ushort)59)]
    public void Root_repeats_signed_target_not_header_group_and_wait_decrements_before_fade(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint address = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x29, 4).WriteByte(0x63, 0).WriteByte(0x2B, 255).WriteByte(0x2A, 0));
        NativeBattleRecoveryFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleRecoveryController.Advance(frame, fixture.Inputs, Resources(effect)).Frame;
        NativeBattleModulePoolTask root = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        Assert.Equal((byte)0, root.ReadByte(0x2B));
        Assert.Equal((byte)0, root.ReadByte(0x2A));
        Assert.Equal((byte)3, root.ReadByte(0x29));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(address, root.WriteByte(0x29, 5).WriteWord(0x60, 0));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleRecoveryController.Advance(frame, fixture.Inputs, Resources(effect), (current, _) => new(current, 0)).Frame;
        Assert.Equal((byte)6, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadByte(0x29));
        Assert.Contains(frame.Effect.Pools[3].Slots, task => task.Callback == profile.FadeOutCallback);
    }

    [Theory]
    [InlineData((ushort)53)]
    [InlineData((ushort)58)]
    [InlineData((ushort)59)]
    public void Current_bank_and_unknown_channels_round_trip_without_reset_or_cross_profile_alias(ushort effect)
    {
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        byte[] bytes = new byte[profile.ExecutableLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(checked((int)(profile.DirectoryAddress - profile.ExecutableAddress))), 1);
        bytes[0] = 0xA7;
        NativeBattleRecoveryExecutableSource source = new(effect, bytes.ToImmutableArray());
        NativeBattleRecoveryExecutableState state = new(effect, source.Data);
        Assert.Same(state, state.Initialize(source));
        Assert.Throws<NotSupportedException>(() => state.Read(profile.ChannelAddress, 2));
        Assert.Equal((byte)0xA7, state.Read(profile.ExecutableAddress, 1)[0]);
        NativeBattleRecoveryExecutableState clone = JsonSerializer.Deserialize<NativeBattleRecoveryExecutableState>(JsonSerializer.Serialize(state))!;
        Assert.Null(clone.Channels);
        Assert.Throws<NotSupportedException>(() => clone.Read(profile.ChannelAddress, 1));
        Assert.Throws<InvalidDataException>(() => state.Initialize(source with { Effect = effect == 53 ? (ushort)58 : (ushort)53 }));
        NativeBattleRecoveryExecutableState cold = NativeBattleRecoveryExecutableState.FromAuthenticatedColdProcess(source, new byte[profile.ChannelLength].ToImmutableArray());
        Assert.Equal(new byte[2], cold.Read(profile.ChannelAddress, 2));
    }

    [Theory]
    [InlineData((ushort)53)]
    [InlineData((ushort)58)]
    [InlineData((ushort)59)]
    public void Native_target_contact_is_signed_threshold_release_is_equality_and_sound_is_its_own_counter(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        NativeBattleRecoveryFrame MakeTarget(byte phase, ushort counter)
        {
            NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
            uint root = hierarchy.Address(0, 0);
            hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10).WriteByte(0x63, 1));
            NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, profile.TargetCallback, 88, root, (pointer, group, selected) => NativeBattleCureController.SelectActors(fixture.Frame.Effect.Parameters!, fixture.Inputs.Packets, pointer, group, selected));
            hierarchy = target.Hierarchy.Write(target.Address, target.Hierarchy.Read(target.Address).WriteByte(0x29, phase).WriteWord(0x24, counter));
            return fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        }
        NativeBattleRecoveryFrame contact = MakeTarget(2, unchecked((ushort)(profile.ContactCounter - 1)));
        NativeBattleRecoveryControllerStep waiting = NativeBattleRecoveryController.Advance(contact, fixture.Inputs, Resources(effect));
        Assert.Empty(waiting.Contacts);
        NativeBattleRecoveryControllerStep reached = NativeBattleRecoveryController.Advance(waiting.Frame, fixture.Inputs, Resources(effect));
        Assert.Equal(new[] { 0 }, reached.Contacts);
        Assert.Empty(NativeBattleRecoveryController.Advance(reached.Frame, fixture.Inputs, Resources(effect)).Contacts);
        NativeBattleRecoveryFrame release = MakeTarget(1, unchecked((ushort)(profile.TargetReleaseCounter + 1)));
        release = NativeBattleRecoveryController.Advance(release, fixture.Inputs, Resources(effect)).Frame;
        Assert.Equal((byte)1, release.Effect.Pools[0].Slots[0].ReadByte(0x63));
        release = MakeTarget(1, profile.TargetReleaseCounter);
        release = NativeBattleRecoveryController.Advance(release, fixture.Inputs, Resources(effect)).Frame;
        Assert.Equal((byte)0, release.Effect.Pools[0].Slots[0].ReadByte(0x63));
        NativeBattleRecoveryFrame sound = MakeTarget(3, unchecked((ushort)(profile.SoundCounter - 1)));
        NativeBattleRecoveryControllerStep beforeSound = NativeBattleRecoveryController.Advance(sound, fixture.Inputs, Resources(effect));
        Assert.Empty(beforeSound.Sounds);
        NativeBattleRecoveryControllerStep played = NativeBattleRecoveryController.Advance(beforeSound.Frame, fixture.Inputs, Resources(effect));
        Assert.Equal(Resources(effect).Sound, Assert.Single(played.Sounds).Sound);
        Assert.Empty(NativeBattleRecoveryController.Advance(played.Frame, fixture.Inputs, Resources(effect)).Sounds);
    }

    private static NativeBattleRecoveryResources Resources(ushort effect) => new(effect, effect == 53 ? 300170u : 300182u, new(effect, []));

    internal static (NativeBattleRecoveryFrame Frame, NativeBattleRecoveryControllerInputs Inputs) Fixture(ushort effect, byte targets = 1)
    {
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, (ushort)1, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(targets);
        NativeBattleOriginalEffectState selected = new(effect, checked((short)(effect - 1)), new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.InitializerGlobal, 256)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(effect - 1);
        memory = memory with { Arena = memory.Arena!.Write(256 + 2 * profile.DrawBankBytes, Enumerable.Repeat((byte)0xA5, profile.EmitterBytes + profile.GeometryBytes).ToArray()) };
        NativeBattleRecoveryFrame frame = NativeBattleRecoveryController.Initialize(memory, selected, packets, Resources(effect));
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        return (frame, new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source));
    }
}
