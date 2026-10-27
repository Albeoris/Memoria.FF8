using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRecoveryMegaPhoenixTests
{
    [Theory]
    [InlineData((ushort)0, (sbyte)0, (ushort)8, (ushort)8, 128)]
    [InlineData((ushort)127, (sbyte)0, (ushort)7, (ushort)7, 128)]
    [InlineData((ushort)0xFFFF, (sbyte)0, (ushort)7, (ushort)7, 128)]
    [InlineData((ushort)127, (sbyte)1, (ushort)127, (ushort)0, 256)]
    [InlineData((ushort)127, (sbyte)2, (ushort)127, (ushort)0, 256)]
    [InlineData((ushort)127, (sbyte)-1, (ushort)127, (ushort)0, 256)]
    public void Mesh_scroll_uses_only_selector_zero_and_low_byte_plus_eight(ushort counter, sbyte selector, ushort next, ushort vertical, int window)
    {
        NativeBattleEsunaMeshOperands prepared = NativeBattleRecoveryMeshPackets.Prepare8CF4C0(counter, selector, 0, 37, 0x123456);
        Assert.Equal(next, prepared.NextUvCounter);
        Assert.Equal(vertical, prepared.Context.VOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, checked((short)window), checked((short)window)), prepared.Context.WindowAfter);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 256, 256), prepared.Context.WindowBefore);
        Assert.Equal(0xF0u, prepared.Context.Flags);
        Assert.Equal((uint?)0x123456, prepared.Context.FarColor);
        Assert.Equal(37, prepared.Context.Fade);
        NativeBattleEsunaMeshOperands unfaded = NativeBattleRecoveryMeshPackets.Prepare8CF4C0(counter, selector, 1, 0, 0x123456);
        Assert.Equal(0u, unfaded.Context.Flags);
        Assert.Null(unfaded.Context.FarColor);
        Assert.Null(unfaded.Context.Fade);
    }

    [Fact]
    public void Target_release_equality_precedes_single_signed_contact_and_sound()
    {
        var fixture = NativeBattleRecoveryControllerTests.Fixture(7);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.MegaPhoenix;
        NativeBattleRecoveryResources resources = new(7, 300185, new(7, []));
        NativeBattleRecoveryFrame baseline = NativeBattleStorageSnapshots.Capture(fixture.Frame);
        NativeBattleRecoveryFrame Target(byte phase, ushort counter)
        {
            fixture.Frame = NativeBattleStorageSnapshots.Restore(baseline);
            NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
            uint root = hierarchy.Address(0, 0);
            hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 10).WriteByte(0x63, 1));
            NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(1, profile.TargetCallback, 88, root, (pointer, group, target) => NativeBattleCureController.SelectActors(fixture.Frame.Effect.Parameters!, fixture.Inputs.Packets, pointer, group, target));
            hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteByte(0x29, phase).WriteWord(0x24, counter));
            return fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        }
        NativeBattleRecoveryControllerStep before = NativeBattleRecoveryController.Advance(Target(1, 6), fixture.Inputs, resources);
        Assert.Equal((byte)1, before.Frame.Effect.Pools[0].Slots[0].ReadByte(0x63));
        NativeBattleRecoveryControllerStep released = NativeBattleRecoveryController.Advance(before.Frame, fixture.Inputs, resources);
        Assert.Equal((byte)0, released.Frame.Effect.Pools[0].Slots[0].ReadByte(0x63));
        NativeBattleRecoveryControllerStep missed = NativeBattleRecoveryController.Advance(Target(1, 8), fixture.Inputs, resources);
        Assert.Equal((byte)1, missed.Frame.Effect.Pools[0].Slots[0].ReadByte(0x63));
        NativeBattleRecoveryControllerStep waiting = NativeBattleRecoveryController.Advance(Target(2, 9), fixture.Inputs, resources);
        Assert.Empty(waiting.Contacts);
        NativeBattleRecoveryControllerStep contact = NativeBattleRecoveryController.Advance(waiting.Frame, fixture.Inputs, resources);
        Assert.Equal(new[] { 0 }, contact.Contacts);
        Assert.Empty(NativeBattleRecoveryController.Advance(contact.Frame, fixture.Inputs, resources).Contacts);
        Assert.Empty(NativeBattleRecoveryController.Advance(Target(2, 0xFFFF), fixture.Inputs, resources).Contacts);
        NativeBattleRecoveryControllerStep sound = NativeBattleRecoveryController.Advance(Target(3, 0), fixture.Inputs, resources);
        Assert.Equal(300185u, Assert.Single(sound.Sounds).Sound);
        Assert.Empty(NativeBattleRecoveryController.Advance(sound.Frame, fixture.Inputs, resources).Sounds);
    }

    [Fact]
    public void Clear_keeps_native_capacities_and_context_survives_snapshot_without_bank_reconstruction()
    {
        var fixture = NativeBattleRecoveryControllerTests.Fixture(7);
        NativeBattleRecoveryResources resources = new(7, 300185, new(7, []));
        NativeBattleRecoveryFrame frame = fixture.Frame;
        Assert.Equal(new[] { 2, 4, 4, 10 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(256 + 0x14000, frame.State.AuxiliaryCursor);
        for (int callback = 0; callback < 4; callback++)
        {
            frame = NativeBattleRecoveryController.Advance(frame, fixture.Inputs, resources, (current, _) => new(current, 0)).Frame;
        }
        Assert.Equal(25 * 108, NativeBattleRecoveryProfile.MegaPhoenix.EmitterBytes);
        Assert.Equal(80 * 672, NativeBattleRecoveryProfile.MegaPhoenix.GeometryBytes);
        Assert.All(frame.Memory.Arena!.Read(frame.State.Emitters!.Value, 0xA8C + 0xD200), value => Assert.Equal((byte)0, value));
        NativeBattleModulePoolTask context = Assert.Single(frame.Effect.Pools[2].Slots, slot => slot.Callback == 0x8CE910);
        Assert.Equal(0x163E198u, context.ReadDword(0x30));
        Assert.Equal((ushort)45, context.ReadWord(0x29E));
        NativeBattleRecoveryFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleRecoveryFrame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleStorageSnapshots.Capture(context).Payload.ToArray(), NativeBattleStorageSnapshots.Capture(Assert.Single(restored.Effect.Pools[2].Slots, slot => slot.Callback == 0x8CE910)).Payload.ToArray());
        Assert.Null(restored.Memory.GetRecoveryExecutable(7));
    }
}
