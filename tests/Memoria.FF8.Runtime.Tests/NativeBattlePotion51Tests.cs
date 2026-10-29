using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePotion51Tests
{
    [Fact]
    public void HiPotion_keeps_native_banks_callback_owners_and_distinct_retained_fields()
    {
        var fixture = Fixture(2);
        NativeBattleCuraFrame frame = fixture.Frame;
        Assert.Equal(new uint[] { 0x261A048, 0x26315E8, 0x2631760, 0x262B1C8 }, frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(new uint[] { 0x2619F80, 0x26315F8, 0x2631770, 0x261AFF8 }, frame.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Equal(new ushort[] { 100, 88, 200, 132 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 64, 500 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(0x261A048u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x81C8E0u, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x2C));
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x2D));
        Assert.Equal(0, frame.State.OddDrawBase26FD114);
        Assert.Equal(0x12000, frame.State.EvenDrawBase26FD110);
        NativeBattleCuraFamilyProfile profile = NativeBattleCuraFamilyProfile.ForEffect(51);
        Assert.Equal(0x2634AB4u, profile.ContextF4Address);
        Assert.Equal(0x26349A8u, profile.SceneFadeAddress);
        Assert.Equal((short?)-7, profile.ReadContextF4(frame.Memory));
        Assert.Equal((int?)99, profile.ReadSceneFade(frame.Memory));
        Assert.Equal((short?)1, NativeBattleCuraFamilyProfile.ForEffect(25).ReadContextF4(frame.Memory));
        Assert.Equal((int?)88, NativeBattleCuraFamilyProfile.ForEffect(25).ReadSceneFade(frame.Memory));
        Assert.Throws<NotSupportedException>(() => profile.CanonicalParticleCallback(0x88A070));
        Assert.Throws<NotSupportedException>(() => NativeBattleCuraFamilyProfile.ForEffect(52));
        Assert.Throws<InvalidDataException>(() => NativeBattleCuraController.Initialize889D60(frame.Memory, frame.Effect, fixture.Inputs.Packets, Resources() with { Effect = 25 }));
    }

    [Fact]
    public void HiPotion_same_pass_preserves_large_then_sprite_pool_order_and_original_C4_angles()
    {
        var fixture = Fixture();
        List<uint> callbacks = [];
        NativeBattleCuraLeafStep Leaf(NativeBattleCuraFrame current, uint address)
        {
            callbacks.Add(new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
            return new(current, 0);
        }
        NativeBattleCuraFrame frame = NativeBattleCuraController.Advance889E40(fixture.Frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Empty(callbacks);
        Assert.Equal(0x12000, frame.State.PrimaryDrawCursor);
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal(0, frame.State.PrimaryDrawCursor);
        Assert.Equal(new uint[] { 0x81D1E0, 0x81DFD0, 0x81E2B0, 0x81DE70, 0x81CB10 }, callbacks);
        callbacks.Clear();
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal(new uint[] { 0x81D1E0, 0x81DFD0, 0x81E2B0, 0x81DE70, 0x81D1E0, 0x81E670, 0x81CB10 }, callbacks);
        Assert.Equal((ushort)0x800, frame.Effect.Pools[2].Slots[4].ReadWord(0xC4));
        Assert.Equal(0x12000, frame.State.PrimaryDrawCursor);
    }

    [Fact]
    public void HiPotion_contact_waits_for_signed_counter_greater_than30_and_preserves_cold_next_callback()
    {
        var fixture = Fixture();
        NativeBattleCuraFrame frame = fixture.Frame;
        NativeBattleCuraLeafStep Leaf(NativeBattleCuraFrame current, uint _) => new(current, 0);
        List<NativeBattleModuleSound> sounds = [];
        for (int callback = 1; callback <= 32; callback++)
        {
            NativeBattleCuraControllerStep step = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Leaf);
            Assert.Empty(step.Contacts);
            sounds.AddRange(step.Sounds);
            frame = step.Frame;
        }
        NativeBattleCuraFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuraFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleCuraControllerStep next = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Leaf);
        NativeBattleCuraControllerStep independent = NativeBattleCuraController.Advance889E40(restored, fixture.Inputs, Resources(), Leaf);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(independent));
        Assert.Equal(new[] { 0 }, next.Contacts);
        Assert.Equal(300077u, Assert.Single(sounds).Sound);
        Assert.Equal((short?)-7, next.Frame.Memory.Potion51ContextF4);
        Assert.Equal((int?)99, next.Frame.Memory.Potion51SceneFade);
    }

    [Fact]
    public void HiPotion_signed_group_progression_and_previous_survivors_govern_release()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 2).WriteByte(0x63, 0).WriteByte(0x2A, 255).WriteWord(0x58, 0));
        NativeBattleCuraFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleCuraLeafStep NoLeaf(NativeBattleCuraFrame _, uint __) => throw new InvalidOperationException("No child was appended.");
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), NoLeaf).Frame;
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x2A));
        Assert.Equal((byte)1, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 3).WriteWord(0x5E, 1));
        frame = NativeBattleCuraController.Advance889E40(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, fixture.Inputs, Resources(), NoLeaf).Frame;
        Assert.Equal(0x261A048u, frame.Effect.ReturnedDescriptor);
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), NoLeaf).Frame;
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void HiPotion_burst_consumes_ordered_jitters_and_stores_actual_child_and_sprite_pointers()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, 0x81C9E0, 88, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleEffectHierarchyAppend burst = target.Hierarchy.Append8DC540(3, 0x81CB10, 132, target.Address, (_, _, _) => new(0, 3));
        NativeBattleCuraFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = burst.Hierarchy.Pools } };
        List<NativeBattleCuraOperation> operations = [];
        NativeCrtRandomState initial = NativeCrtRandomState.FromColdProcess();
        NativeBattleCuraParticleInputs inputs = new(fixture.Inputs, initial, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        NativeBattleCuraParticleStep step = NativeBattleCuraParticles.AdvanceSprite(frame, burst.Address, Resources(), inputs);
        NativeCrtRandomState expected = initial.Draw(out ushort baseAngle);
        NativeBattleModulePoolTask[] children = step.Frame.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x81CD70).ToArray();
        Assert.Equal(8, children.Length);
        for (int index = 0; index < children.Length; index++)
        {
            expected = expected.Draw(out ushort jitter);
            Assert.Equal((ushort)(((baseAngle + index * 512) & 0xFFF) + (jitter & 127)), children[index].ReadWord(0x42));
        }
        Assert.Equal(expected, step.CrtRandomAfter);
        Assert.Equal(2, operations.Count);
        Assert.All(operations, operation => Assert.Equal(0x15A9F78u, Assert.IsType<NativeBattleCuraSpriteOperation>(operation).Draw.Resource!.Address));
        NativeBattleModuleDraw[] drawReceipts = operations.Cast<NativeBattleCuraSpriteOperation>().Select(operation => operation.Draw).ToArray();
        NativeBattlePilotVisualIdentity taskIdentity = NativeBattlePilotVisualLifetimes.Identity(51, burst.Hierarchy.Read(burst.Address));
        Assert.All(drawReceipts, draw =>
        {
            NativeBattlePilotVisualIdentity identity = Assert.IsType<NativeBattlePilotVisualIdentity>(draw.PilotIdentity);
            Assert.Equal((ushort)51, identity.Effect);
            Assert.Equal(taskIdentity.Generation, identity.Generation);
            Assert.Equal(taskIdentity.Bank, identity.Bank);
            Assert.Equal(NativeBattleTestSnapshots.Json(draw with { PilotIdentity = null }), NativeBattleTestSnapshots.Json(draw));
        });
        Assert.NotEqual(drawReceipts[0].PilotIdentity, drawReceipts[1].PilotIdentity);
    }

    private static (NativeBattleCuraFrame Frame, NativeBattleCuraControllerInputs Inputs) Fixture(byte actor = 0)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, int header) = packets.Prepare(new(actor, 4, 0, 0, 1, 51, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(header, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(51, 50, new(51, 0x81C800, 0x81C7E0), new(actor, 1, 8, header, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Textures = null, CuraContextF4 = 1, CuraSceneFade = 88, Potion51ContextF4 = -7, Potion51SceneFade = 99 };
        NativeBattleCuraFrame frame = NativeBattleCuraController.Initialize889D60(memory, effect, packets, Resources());
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        return (frame, new(packets, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => pose));
    }

    private static NativeBattleCuraResources Resources()
    {
        NativeBattleCuraFamilyProfile profile = NativeBattleCuraFamilyProfile.ForEffect(51);
        uint[] addresses = [0x161079C, 0x1610948, 0x16109BC, 0x1610A10, 0x1610AD4, 0x1610C00, 0x1610DAC, 0x1610F58];
        int[] frames = [16, 4, 2, 7, 11, 16, 16, 16];
        return new(new("synthetic-registration-unused", []), addresses.Select((address, index) => Sprite(profile.Resource(address), frames[index])).ToImmutableArray(), 300077) { Effect = 51 };
    }

    private static NativeBattleModuleResource Sprite(uint address, int frames)
    {
        int offset = 12 + frames * 2;
        byte[] bytes = new byte[offset + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)frames);
        for (int frame = 0; frame <= frames; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
        }
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
