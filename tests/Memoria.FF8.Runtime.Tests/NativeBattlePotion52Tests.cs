using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePotion52Tests
{
    [Fact]
    public void XPotion_owns_four_pools_and_clears_auxiliary_arrays_without_allocating_a_fade()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = fixture.Frame;
        Assert.Equal(new uint[] { 0x2619588, 0x2619DA8, 0x26193D0, 0x2619D98 }, frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(new uint[] { 0x26194C0, 0x2619DB8, 0x26193E0, 0x26195A8 }, frame.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Equal(new ushort[] { 100, 88, 60, 676 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3, 3 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        List<uint> callbacks = [];
        for (int callback = 1; callback <= 4; callback++)
        {
            frame = NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), (current, address) =>
            {
                callbacks.Add(new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
                return new(current, 0);
            }).Frame;
            Assert.Equal(256 + ((callback & 1) == 1 ? 0x6000 : 0), frame.State.PrimaryDrawCursor);
            Assert.Equal(256 + ((callback & 1) == 1 ? 0xC000 : 0x6000), frame.State.SecondaryDrawCursor);
        }
        Assert.Equal(new uint[] { 0x817980 }, callbacks);
        Assert.Equal(256 + 0xC000, frame.State.Auxiliary25F8);
        Assert.Equal(256 + 0xC000 + 0x25F8, frame.State.Auxiliary16F80);
        Assert.Equal(256 + 0xC000 + 0x25F8 + 0x16F80, frame.State.AuxiliaryCursor);
        Assert.All(frame.Memory.Arena!.Read(frame.State.Auxiliary25F8!.Value, 0x25F8 + 0x16F80), value => Assert.Equal((byte)0, value));
        NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[3].Slots.Where(task => task.Callback == 0x817980));
        Assert.Equal(0x17AB0E8u, generic.ReadDword(0x30));
        Assert.Equal((ushort)45, generic.ReadWord(0x29E));
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaFamilyProfile.ForEffect(52).Callback(0x87DE50));
    }

    [Fact]
    public void XPotion_flash_counter20_equality30_and_next_phase_contact_remain_distinct()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = fixture.Frame;
        List<NativeBattleModuleSound> sounds = [];
        NativeBattleCuragaLeafStep Leaf(NativeBattleCuragaFrame current, uint _) => new(current, 0);
        for (int callback = 1; callback <= 35; callback++)
        {
            NativeBattleCuragaControllerStep step = NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), Leaf);
            Assert.Equal(callback == 35 ? new[] { 0 } : [], step.Contacts);
            sounds.AddRange(step.Sounds);
            frame = step.Frame;
            if (callback == 24)
            {
                NativeBattleModulePoolTask flash = Assert.Single(frame.Effect.Pools[2].Slots.Where(task => task.Callback == 0x817800));
                Assert.Equal(0x15A9208u, flash.ReadDword(0x30));
            }
            if (callback == 34)
            {
                Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x63));
                NativeBattleCuragaFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuragaFrame>(NativeBattleTestSnapshots.Json(frame))!;
                Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleCuragaController.Advance87DC50(NativeBattleTestSnapshots.Independent(frame), fixture.Inputs, Resources(), Leaf)), NativeBattleTestSnapshots.Json(NativeBattleCuragaController.Advance87DC50(restored, fixture.Inputs, Resources(), Leaf)));
            }
        }
        Assert.Equal(300143u, Assert.Single(sounds).Sound);
    }

    [Fact]
    public void XPotion_signed_group_and_no_timer_phase_preserve_native_partial_writes_and_prior_survivors()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4).WriteByte(0x2A, 255).WriteWord(0x60, 0xABCD));
        NativeBattleCuragaFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleCuragaLeafStep Leaf(NativeBattleCuragaFrame current, uint _) => new(current, 0);
        frame = NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x2A));
        Assert.Equal((byte)1, frame.Effect.Pools[0].Slots[0].ReadByte(0x2E));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4));
        frame = NativeBattleCuragaController.Advance87DC50(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal((byte)5, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0xABCD, frame.Effect.Pools[0].Slots[0].ReadWord(0x60));
        frame = NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0xABCD, frame.Effect.Pools[0].Slots[0].ReadWord(0x60));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteWord(0x5E, 1));
        frame = NativeBattleCuragaController.Advance87DC50(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        for (int index = 0; index < 4; index++)
        {
            frame = NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), Leaf).Frame;
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void XPotion_retained_fixups_use_actual_base_once_and_cannot_borrow_the_Curaga_owner()
    {
        byte[] bytes = new byte[NativeBattleCuragaExecutableState.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x1000);
        NativeBattleCuragaExecutableSource source = new(bytes.ToImmutableArray()) { ResourceAddress = 0x17AB0E8 };
        NativeBattleCuragaExecutableState retained = NativeBattleCuragaExecutableState.FromColdProcess(52).Initialize87EA20(source);
        Assert.Equal(0x17AC0E8u, BinaryPrimitives.ReadUInt32LittleEndian(retained.Read(0x17AB0EC, 4)));
        Assert.Equal(0x17AB0E8u, BinaryPrimitives.ReadUInt32LittleEndian(retained.Read(0x17AC1B0, 4)));
        Assert.Same(retained, retained.Initialize87EA20(source));
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(source));
        Assert.Throws<NotSupportedException>(() => (new NativeBattleCuragaExecutableState(1, null) { ResourceAddress = 0x17AB0E8 }).Initialize87EA20(source));
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForEffect(52);
        NativeBattleEffectMemory memory = profile.WithExecutable(NativeBattleEffectMemory.FromColdProcess(), retained);
        Assert.Equal(0u, memory.CuragaExecutable!.InitializationFlag);
        Assert.Same(retained, profile.RequireExecutable(memory));
        Assert.Throws<NotSupportedException>(() => profile.RequireExecutable(memory with { Potion52Executable = null }));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Equal(retained.Read(0x17AB0E8, NativeBattleCuragaExecutableState.Length), profile.RequireExecutable(restored).Initialize87EA20(source).Read(0x17AB0E8, NativeBattleCuragaExecutableState.Length));
    }

    [Fact]
    public void XPotion_entry_reads_signed_header_and_contact_selectors_but_counts_from_the_parameter_header()
    {
        var fixture = Fixture();
        NativeBattleActionPacketsState packets = fixture.Inputs.Packets;
        NativeBattleActionHeaderSlot selected = packets.Headers[0] with { FirstContactIndex = 2 };
        NativeBattleActionHeaderSlot parameter = selected with { Payload = selected.Payload.SetItem(0, (byte)6).SetItem(16, (byte)4).SetItem(17, (byte)2), UnknownByteMask = selected.UnknownByteMask & ~((1u << 0) | (1u << 16) | (1u << 17)) };
        packets = packets with { Headers = packets.Headers.SetItem(0, selected).SetItem(1, parameter) };
        NativeBattleModulePoolTask task = fixture.Frame.Effect.Pools[0].Slots[0].WriteByte(0x2A, 255).WriteByte(0x2B, 254).WriteByte(0x29, 9).WriteWord(0x60, 0xABCD);
        NativeBattleEffectParameters parameters = fixture.Frame.Effect.Parameters! with { HeaderIndex7C = 1 };
        NativeBattleModulePoolTask initialized = NativeBattleCuragaController.InitializeXPotionRoot817410(task, parameters, packets);
        Assert.Equal((byte)2, initialized.ReadByte(0x2C));
        Assert.Equal((byte)3, initialized.ReadByte(0x2D));
        Assert.Equal((byte)0, initialized.ReadByte(0x29));
        Assert.Equal((ushort)4, initialized.ReadWord(0x5A));
        Assert.Equal((ushort)2, initialized.ReadWord(0x58));
        Assert.Equal((byte)3, initialized.ReadByte(0x2F));
        Assert.Equal((byte)255, initialized.ReadByte(0x2A));
        Assert.Equal((byte)254, initialized.ReadByte(0x2B));
        Assert.Equal((ushort)0xABCD, initialized.ReadWord(0x60));
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaController.InitializeXPotionRoot817410(task, parameters with { HeaderIndex7C = 0 }, packets));
    }

    [Fact]
    public void XPotion_presentation_flash_uses_its_actual_task_identity_and_stays_discrete_and_read_only()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend flash = hierarchy.Append8DC540(2, 0x817800, 60, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = flash.Hierarchy.Write(flash.Address, flash.Hierarchy.Read(flash.Address).WriteDword(0x30, 0x15A9208).WriteWord(0x38, 0));
        NativeBattleCuragaFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleCuragaResources resources = Resources() with { FlashProgram = [0x007F7F7F, 0xFF000000] };
        NativeBattleCuragaFrame independentBeforeCallback = NativeBattleTestSnapshots.Independent(frame);
        List<NativeBattleCuragaOperation> operations = [];
        NativeBattleCuragaLeafStep drawn = NativeBattleCuragaParticles.AdvanceSimple(frame, flash.Address, resources, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        NativeBattleModulePacketBatch batch = Assert.IsType<NativeBattleCuragaPacketOperation>(Assert.Single(operations)).Batch;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(batch.Primitives[0].Primitive.PilotGeometry);
        Assert.Equal(NativeBattlePilotVisualLifetimes.Identity(52, hierarchy.Read(flash.Address)), geometry.Identity);
        Assert.False(geometry.Continuous);
        Assert.True(geometry.ScreenSpace);
        Assert.Equal(8, geometry.ScreenScale);
        Assert.Null(batch.Primitives[1].Primitive.PilotGeometry);
        string native = NativeBattleTestSnapshots.Json(drawn);
        Assert.Same(geometry, NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { A = new(100, 100, 100) }, 0.5f));
        Assert.Equal(native, NativeBattleTestSnapshots.Json(drawn));
        NativeBattleEffectHierarchy drawnHierarchy = new(drawn.Frame.Effect.Pools);
        Assert.Equal((ushort)1, drawnHierarchy.Read(flash.Address).ReadWord(0x38));
        Assert.Equal((byte)1, drawnHierarchy.Read(hierarchy.Address(0, 0)).ReadByte(0x28));
        Assert.Equal((ushort)0, new NativeBattleEffectHierarchy(independentBeforeCallback.Effect.Pools).Read(flash.Address).ReadWord(0x38));
        NativeBattleCuragaLeafStep restored = NativeBattleCuragaParticles.AdvanceSimple(independentBeforeCallback, flash.Address, resources, (current, _) => current);
        Assert.Equal(native, NativeBattleTestSnapshots.Json(restored));
    }

    private static NativeBattleCuragaResources Resources() => new(new("skipped-camera-registration", []), 300143) { Effect = 52 };

    private static (NativeBattleCuragaFrame Frame, NativeBattleCuragaControllerInputs Inputs) Fixture(byte targets = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 23, 52, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(targets);
        NativeBattleOriginalEffectState selected = new(52, 51, new(52, 0x817410, 0x8173F0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x2619494, 256)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(51);
        memory = memory with { Potion52Executable = NativeBattleCuragaExecutableState.FromColdProcess(52), Arena = memory.Arena!.Write(256 + 0xC000, Enumerable.Repeat((byte)0xA5, 0x25F8 + 0x16F80).ToArray()) };
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(memory, selected, packets, Resources());
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        return (frame, new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source));
    }
}
