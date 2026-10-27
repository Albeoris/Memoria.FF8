using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePotion50Tests
{
    [Fact]
    public void Potion_keeps_its_original_pools_callbacks_and_retained_context_separate_from_Cure()
    {
        var fixture = Fixture();
        NativeBattleCureFrame frame = fixture.Frame;
        Assert.Equal(new uint[] { 0x2634BB8, 0x2643E20, 0x2635B68, 0x2646380 }, frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(new uint[] { 0x2634AF0, 0x2646220, 0x2635B78, 0x2643E30 }, frame.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Equal(new ushort[] { 100, 88, 108, 92 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 300, 100 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(0x2634BB8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x81FB40u, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal((short?)1, frame.Memory.CureContextF4);
        Assert.Equal((short?)-7, frame.Memory.Potion50ContextF4);
        NativeBattleCureFamilyProfile profile = NativeBattleCureFamilyProfile.ForEffect(50);
        Assert.Equal(0x26464D4u, profile.ContextF4Address);
        Assert.Equal(0x8DCC20u, profile.Callback(0x8DCC20));
        Assert.Throws<NotSupportedException>(() => profile.CanonicalParticleCallback(0x8D6D80));
        Assert.Throws<NotSupportedException>(() => NativeBattleCureFamilyProfile.ForEffect(51));
        NativeBattleOriginalEffectState wrongPointer = frame.Effect with { Pools = [], ReturnedDescriptor = 0, ResourcePointers = [new(0x277AEC0, 0)] };
        Assert.Throws<NotSupportedException>(() => NativeBattleCureController.Initialize8D6A00(frame.Memory, wrongPointer, fixture.Inputs.Packets, Resources()));
        Assert.Throws<InvalidDataException>(() => NativeBattleCureController.Initialize8D6A00(frame.Memory, frame.Effect, fixture.Inputs.Packets, Resources() with { Effect = 1 }));
    }

    [Fact]
    public void Potion_children_start_in_the_same_pass_and_contact_at_target_counter15_after_cold_restore()
    {
        var fixture = Fixture();
        NativeBattleCureFrame frame = fixture.Frame;
        int leaves = 0;
        List<int> contacts = [];
        List<NativeBattleModuleSound> sounds = [];
        for (int callback = 1; callback <= 19; callback++)
        {
            NativeBattleCureFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCureFrame>(NativeBattleTestSnapshots.Json(frame))!;
            NativeBattleCureControllerStep step = Advance(frame, true);
            NativeBattleCureControllerStep independent = Advance(restored, false);
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(independent));
            frame = step.Frame;
            contacts.AddRange(step.Contacts);
            sounds.AddRange(step.Sounds);
            Assert.Equal(0x2634BB8u, frame.Effect.ReturnedDescriptor);
            Assert.Equal(callback % 2 == 1 ? 0x10000 : 0, frame.State.PrimaryDrawCursor);
            Assert.Equal(callback % 2 == 1 ? 0x20000 : 0x10000, frame.State.SecondaryDrawCursor);
            if (callback < 4)
            {
                Assert.Equal(0, leaves);
            }
            if (callback < 19)
            {
                Assert.Empty(contacts);
            }
        }
        Assert.Equal(16, leaves);
        Assert.Equal(new[] { 0 }, contacts);
        Assert.Equal(300018u, Assert.Single(sounds).Sound);
        Assert.Equal(0x81FCD0u, frame.Effect.Pools[1].Slots[0].Callback);
        Assert.Equal((short?)-7, frame.Memory.Potion50ContextF4);

        NativeBattleCureControllerStep Advance(NativeBattleCureFrame current, bool observe)
        {
            return NativeBattleCureController.Advance8D6B30(current, fixture.Inputs, Resources(), (leaf, address) =>
            {
                Assert.Equal(0x81FD90u, new NativeBattleEffectHierarchy(leaf.Effect.Pools).Read(address).Callback);
                if (observe)
                {
                    leaves++;
                }
                return new(leaf, 0);
            });
        }
    }

    [Fact]
    public void Potion_root_release_uses_its_own_descriptor_after_the_native_completion_phase()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 9));
        NativeBattleCureFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleCureControllerStep step = NativeBattleCureController.Advance8D6B30(frame, fixture.Inputs, Resources(), (_, _) => throw new InvalidOperationException("The completed root has no children."));
        Assert.Equal(0u, step.Frame.Effect.ReturnedDescriptor);
        Assert.Null(step.Frame.Effect.Pools[0].Head);
    }

    [Theory]
    [InlineData(0x820F70u, 1, 18, 0x15AD610u)]
    [InlineData(0x8210C0u, 3, 10, 0x15AD7ECu)]
    public void Potion_moving_particles_keep_original_resource_pointers_rng_order_and_inclusive_lifetime(uint callback, int randomDraws, int callbacks, uint spriteAddress)
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, 0x81FCD0, 88, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleEffectHierarchyAppend particle = target.Hierarchy.Append8DC540(2, callback, 108, target.Address, (_, _, _) => new(0, 3));
        NativeBattleCureFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = particle.Hierarchy.Pools } };
        NativeCrtRandomState expected = NativeCrtRandomState.FromColdProcess();
        for (int draw = 0; draw < randomDraws; draw++)
        {
            expected = expected.Draw(out _);
        }
        List<NativeBattleCureOperation> operations = [];
        NativeBattleCureParticleInputs inputs = new(fixture.Inputs, NativeCrtRandomState.FromColdProcess(), (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        NativeBattleCureParticleStep step = NativeBattleCureParticles.AdvancePrivate(frame, particle.Address, Resources(), inputs);
        Assert.Equal(expected, step.CrtRandomAfter);
        Assert.Equal(spriteAddress, Assert.IsType<NativeBattleCureSpriteOperation>(Assert.Single(operations)).Draw.Resource!.Address);
        int count = 1;
        while (step.Result == 0 && count < 30)
        {
            operations.Clear();
            step = NativeBattleCureParticles.AdvancePrivate(step.Frame, particle.Address, Resources(), inputs with { CrtRandom = expected });
            Assert.Equal(expected, step.CrtRandomAfter);
            count++;
        }
        Assert.Equal(callbacks, count);
        Assert.Equal(2, step.Result);
        Assert.Empty(operations);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(target.Address).ReadByte(0x28));
    }

    private static (NativeBattleCureFrame Frame, NativeBattleCureControllerInputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 4, 0, 0, 0, 50, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(50, 0, new(50, 0x81FA10, 0x81F9F0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x2634AB8, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0) with { Textures = null, CureContextF4 = 1, Potion50ContextF4 = -7 };
        NativeBattleCureFrame frame = NativeBattleCureController.Initialize8D6A00(memory, effect, packets, Resources());
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
        return (frame, new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => pose));
    }

    private static NativeBattleCureResources Resources()
    {
        return new(new("synthetic-animation-unused", []), [Sprite(0x15AD52C, 2), Sprite(0x15AD580, 5), Sprite(0x15AD610, 17), Sprite(0x15AD7EC, 27), Sprite(0x15ADAB8, 1)], 300018) { Effect = 50 };
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
