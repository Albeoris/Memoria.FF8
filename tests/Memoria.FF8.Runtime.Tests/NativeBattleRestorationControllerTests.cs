using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationControllerTests
{
    [Theory]
    [InlineData((ushort)24, 0xA000, 0x10E0, 0x10680)]
    [InlineData((ushort)27, 0x6000, 0x1518, 0x16F80)]
    public void Original_entry_keeps_distinct_physical_pools_banks_and_auxiliary_capacities(ushort effect, int bankBytes, int emitterBytes, int geometryBytes)
    {
        var fixture = Fixture(effect, 0);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        Assert.Equal(new[] { 2, 4, 3, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 88, 676, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(profile.Descriptors.ToArray(), fixture.Frame.Effect.Pools.Select(pool => pool.DescriptorAddress).ToArray());
        Assert.Equal(profile.Banks.ToArray(), fixture.Frame.Effect.Pools.Select(pool => pool.BankAddress).ToArray());
        Assert.Equal(bankBytes, profile.DrawBankBytes);
        Assert.Equal(emitterBytes, profile.EmitterBytes);
        Assert.Equal(geometryBytes, profile.GeometryBytes);
        Assert.Equal(256 + bankBytes * 2, fixture.Frame.State.AuxiliaryCursor);
        Assert.Null(fixture.Frame.State.PrimaryDrawCursor);
        Assert.Null(fixture.Frame.State.CopiedCamera);
        Assert.Null(fixture.Frame.State.GeometryScanCursor);
        Assert.Equal((byte)255, fixture.Frame.Effect.Pools[0].Slots[0].ReadByte(0x2F));
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Clear_precedes_leaf_and_first_generic_child_keeps_own_bank_zero_delay_and_duration45(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleRestorationFrame frame = fixture.Frame;
        List<uint> callbacks = [];
        NativeBattleRestorationLeafStep Leaf(NativeBattleRestorationFrame current, uint address)
        {
            callbacks.Add(new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
            Assert.All(current.Memory.Arena!.Read(current.State.Emitters!.Value, profile.EmitterBytes + profile.GeometryBytes), value => Assert.Equal((byte)0, value));
            return new(current, 0);
        }
        for (int callback = 1; callback <= 4; callback++)
        {
            frame = NativeBattleRestorationController.Advance(frame, fixture.Inputs, Resources(effect), Leaf).Frame;
            Assert.Equal(256 + ((callback & 1) == 1 ? profile.DrawBankBytes : 0), frame.State.PrimaryDrawCursor);
        }
        Assert.Equal(new[] { profile.FadeInCallback, profile.GenericCallback, profile.FadeInCallback }, callbacks);
        Assert.Equal(256 + 2 * profile.DrawBankBytes, frame.State.Emitters);
        Assert.Equal(frame.State.Emitters + profile.EmitterBytes, frame.State.Geometry);
        Assert.Equal(frame.State.Geometry + profile.GeometryBytes, frame.State.AuxiliaryCursor);
        Assert.Equal((ushort)0, frame.State.EmitterScanCursor);
        Assert.Equal((ushort)0, frame.State.GeometryScanCursor);
        NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[2].Slots.Where(task => task.Callback == profile.GenericCallback));
        Assert.Equal(profile.ExecutableAddress, generic.ReadDword(0x30));
        Assert.Equal((ushort)0, generic.ReadWord(0x298));
        Assert.Equal((ushort)0, generic.ReadWord(0x29C));
        Assert.Equal((ushort)45, generic.ReadWord(0x29E));
        NativeBattleRestorationFrame clone = JsonSerializer.Deserialize<NativeBattleRestorationFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleRestorationController.Advance(frame, fixture.Inputs, Resources(effect), Leaf)), JsonSerializer.Serialize(NativeBattleRestorationController.Advance(clone, fixture.Inputs, Resources(effect), Leaf)));
    }

    [Theory]
    [InlineData((ushort)24, 20, 20, 34)]
    [InlineData((ushort)27, 30, 10, 35)]
    public void Target_release_and_root_wait_are_spell_specific_contact_uses_signed30_and_sound_once(ushort effect, int releaseCounter, int rootWait, int contactCallback)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame frame = fixture.Frame;
        List<NativeBattleModuleSound> sounds = [];
        List<int> applied = [];
        NativeBattleRestorationControllerInputs inputs = fixture.Inputs with
        {
            ApplyContact = (current, index) =>
            {
                applied.Add(index);
                NativeBattleEffectTextureState textures = current.Memory.Textures!;
                return current with { Memory = current.Memory with { Textures = textures.EnqueueCopy505EB0(1, 2, 3, 4, 5, 6) } };
            }
        };
        for (int callback = 1; callback <= 35; callback++)
        {
            NativeBattleRestorationControllerStep step = NativeBattleRestorationController.Advance(frame, inputs, Resources(effect), (current, _) =>
            {
                Assert.Equal(applied.Count, current.Memory.Textures!.Count);
                return new(current, 0);
            });
            frame = step.Frame;
            sounds.AddRange(step.Sounds);
            Assert.Equal(callback == contactCallback ? new[] { 0 } : [], step.Contacts);
            if (callback == 4 + releaseCounter)
            {
                Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x63));
            }
            if (callback == 5 + releaseCounter)
            {
                Assert.Equal((ushort)rootWait, frame.Effect.Pools[0].Slots[0].ReadWord(0x60));
            }
        }
        Assert.Equal(new[] { 0 }, applied);
        NativeBattleEffectTimUpload copy = Assert.IsType<NativeBattleEffectTimUpload>(frame.Memory.Textures!.Slots[0]);
        Assert.Equal((byte)3, copy.Kind);
        Assert.Equal(new NativeBattleEffectVramCopy(1, 2, 3, 4, 5, 6), copy.Copy);
        NativeBattleModuleSound sound = Assert.Single(sounds);
        Assert.Equal(Resources(effect).Sound, sound.Sound);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Missing_private_leaf_and_unknown_initialization_owner_fail_without_aliasing_another_spell(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame frame = fixture.Frame;
        for (int callback = 0; callback < 2; callback++)
        {
            frame = NativeBattleRestorationController.Advance(frame, fixture.Inputs, Resources(effect)).Frame;
        }
        Assert.Contains("private context/rendering", Assert.Throws<NotSupportedException>(() => NativeBattleRestorationController.Advance(frame, fixture.Inputs, Resources(effect))).Message);
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationController.Initialize(fixture.Frame.Memory, fixture.Frame.Effect with { ResourcePointers = [] }, fixture.Inputs.Packets, Resources(effect)));
        NativeBattleEffectMemory unknown = fixture.Frame.Memory with { Textures = null };
        Assert.Null(NativeBattleRestorationController.Initialize(unknown, fixture.Frame.Effect, fixture.Inputs.Packets, Resources(effect)).Memory.Textures);
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationController.Initialize(unknown, fixture.Frame.Effect with { Parameters = fixture.Frame.Effect.Parameters! with { Flags79 = 0 } }, fixture.Inputs.Packets, Resources(effect)));
        Assert.Throws<InvalidDataException>(() => NativeBattleRestorationController.Initialize(fixture.Frame.Memory, fixture.Frame.Effect, fixture.Inputs.Packets, Resources(effect == 24 ? (ushort)27 : (ushort)24)));
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Generic_signed_delay_initializes_before_first_update_and_defers_completion_until_next_phase(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame frame = fixture.Frame;
        for (int callback = 0; callback < 4; callback++)
        {
            frame = NativeBattleRestorationController.Advance(frame, fixture.Inputs, Resources(effect), (current, _) => new(current, 0)).Frame;
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint address = hierarchy.Address(2, 0);
        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x24, 0xFFFF).WriteWord(0x298, 0xFFFE));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        List<string> calls = [];
        NativeBattleRestorationGenericInputs inputs = new((current, _) =>
        {
            calls.Add("initialize");
            return current;
        }, (current, _) =>
        {
            calls.Add("advance");
            return new(current, true);
        });
        NativeBattleRestorationLeafStep first = NativeBattleRestorationGeneric.Advance(frame, address, inputs);
        Assert.Equal(new[] { "initialize", "advance" }, calls);
        Assert.Equal(0, first.Result);
        Assert.Equal((ushort)0, new NativeBattleEffectHierarchy(first.Frame.Effect.Pools).Read(address).ReadWord(0x24));
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(first.Frame.Effect.Pools).Read(address).ReadByte(0x29));
        NativeBattleRestorationLeafStep second = NativeBattleRestorationGeneric.Advance(first.Frame, address, inputs);
        Assert.Equal(new[] { "initialize", "advance", "advance" }, calls);
        Assert.Equal(2, second.Result);
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationGeneric.Advance(frame, address));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x24, 0).WriteWord(0x298, 1));
        NativeBattleRestorationFrame waiting = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        Assert.Equal(0, NativeBattleRestorationGeneric.Advance(waiting, address).Result);
    }

    private static NativeBattleRestorationResources Resources(ushort effect) => new(effect, new("skipped-original-animation", []), effect == 24 ? 0x49442u : 0x49445u);

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Root_repeat_compares_signed_group_and_decremented_timer_before_appending_its_own_fade_out(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4).WriteByte(0x63, 0).WriteByte(0x2A, 255));
        NativeBattleRestorationFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleRestorationController.Advance(frame, fixture.Inputs, Resources(effect)).Frame;
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x2A));
        Assert.Equal((byte)1, frame.Effect.Pools[0].Slots[0].ReadByte(0x2E));
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 5).WriteWord(0x60, 0xFFFF));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleRestorationController.Advance(frame, fixture.Inputs, Resources(effect), (current, _) => new(current, 0)).Frame;
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Contains(frame.Effect.Pools[3].Slots, task => task.Callback == profile.FadeOutCallback);
    }

    internal static (NativeBattleRestorationFrame Frame, NativeBattleRestorationControllerInputs Inputs) Fixture(ushort effect, byte targets = 1)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, effect == 24 ? (ushort)27 : (ushort)25, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(targets);
        NativeBattleOriginalEffectState selected = new(effect, checked((short)(effect - 1)), new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.InitializerGlobal, 256)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(effect - 1);
        memory = memory with { Arena = memory.Arena!.Write(256 + 2 * profile.DrawBankBytes, Enumerable.Repeat((byte)0xA5, profile.EmitterBytes + profile.GeometryBytes).ToArray()) };
        NativeBattleRestorationFrame frame = NativeBattleRestorationController.Initialize(memory, selected, packets, Resources(effect));
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        return (frame, new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source));
    }
}
