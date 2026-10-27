using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFireCallbackTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OriginalControllerAndEveryAppendedParticleDrainWithNativeContactsAndSound(int targets)
    {
        var fixture = Fixture(targets);
        NativeBattleOriginalEffectState effect = fixture.Effect;
        NativeBattleFireInputs inputs = fixture.Inputs;
        List<int> contacts = [];
        List<int> appliedContacts = [];
        inputs = inputs with { ApplyContact = appliedContacts.Add };
        List<NativeBattleFireSound> sounds = [];
        HashSet<uint> resources = [];
        int frames = 0;
        while (effect.ReturnedDescriptor != 0 && frames < 100)
        {
            NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, inputs);
            contacts.AddRange(step.Contacts);
            sounds.AddRange(step.Sounds);
            foreach (NativeBattleModuleDraw draw in step.Draws)
            {
                resources.Add(draw.Resource!.Address);
            }
            effect = NativeBattleTestSnapshots.RestoreJson<NativeBattleOriginalEffectState>(NativeBattleTestSnapshots.Json(step.Effect))!;
            inputs = inputs with { CrtRandom = step.CrtRandomAfter };
            frames++;
        }
        Assert.Equal((uint?)0, effect.ReturnedDescriptor);
        Assert.InRange(frames, 22, 65);
        Assert.Equal(Enumerable.Range(0, targets), contacts);
        Assert.Equal(contacts, appliedContacts);
        NativeBattleFireSound sound = Assert.Single(sounds);
        Assert.Equal(0xDEE130u, sound.DescriptorAddress);
        Assert.Equal(256, sound.Volume);
        Assert.Equal(targets == 1 ? null : (int?)128, sound.Pan);
        Assert.Equal(targets == 1, sound.Position is not null);
        Assert.True(resources.SetEquals([0xDEDC2C, 0xDEDDD8, 0xDEDF84, 0x123450]));
        Assert.All(effect.FireModelCompletion!.Value, value => Assert.Equal(1, value));
        Assert.NotEqual(NativeCrtRandomState.FromColdProcess(), inputs.CrtRandom);
        Assert.All(effect.Pools, pool => Assert.All(pool.Slots, slot => Assert.Equal(0, slot.Flags)));
    }

    [Fact]
    public void ControllerPauseAndParticlePausePreserveChildStateAndRandomButKeepDrawingStartedParticles()
    {
        var fixture = Fixture(1);
        NativeBattleFireStep pausedController = NativeBattleFireEffect.Step(fixture.Effect, fixture.Inputs with { GraphicsFlags = 0x200 });
        Assert.Equal(fixture.Inputs.CrtRandom, pausedController.CrtRandomAfter);
        Assert.Equal(fixture.Effect.Pools[1], pausedController.Effect.Pools[1]);
        Assert.Empty(pausedController.Draws);
        NativeBattleOriginalEffectState effect = fixture.Effect;
        NativeBattleFireInputs inputs = fixture.Inputs;
        for (int index = 0; index < 8; index++)
        {
            NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, inputs);
            effect = step.Effect;
            inputs = inputs with { CrtRandom = step.CrtRandomAfter };
        }
        inputs = inputs with { GraphicsFlags = 1 };
        NativeBattleFireStep first = NativeBattleFireEffect.Step(effect, inputs);
        NativeBattleFireStep second = NativeBattleFireEffect.Step(first.Effect, inputs);
        Assert.NotEmpty(first.Draws);
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Draws), NativeBattleTestSnapshots.Json(second.Draws));
        Assert.Equal(inputs.CrtRandom, first.CrtRandomAfter);
        Assert.Equal(NativeBattleTestSnapshots.Json(effect.Pools[1]), NativeBattleTestSnapshots.Json(first.Effect.Pools[1]));
        Assert.Empty(first.Contacts);
        Assert.Empty(first.Sounds);
    }

    [Fact]
    public void LargeParticleEmitsItsGroundTransformAndConsumesExactlyItsFourSmallAndTenSparkInitializers()
    {
        var fixture = Fixture(1);
        NativeBattleEffectTaskPool children = fixture.Effect.Pools[1];
        NativeBattleModulePoolTask task = NativeBattleTaskFixture.EditTask(children.Slots[0], taskData => taskData with { Callback = 0x629C10 });
        task = task.WriteWord(12, 0).WriteWord(14, 0).WriteWord(16, 100).WriteWord(18, unchecked((ushort)-600)).WriteWord(20, 300).WriteWord(22, 25).WriteWord(28, 4096);
        NativeBattleOriginalEffectState effect = fixture.Effect with { Pools = fixture.Effect.Pools.SetItem(1, NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, task) })) };
        NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, fixture.Inputs);
        NativeCrtRandomState expected = fixture.Inputs.CrtRandom;
        for (int index = 0; index < 114; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.CrtRandomAfter);
        Assert.Equal(4, step.Effect.Pools[1].Slots.Count(slot => slot.Flags != 0 && slot.Callback == 0x62A0A0));
        Assert.Equal(10, step.Effect.Pools[1].Slots.Count(slot => slot.Flags != 0 && slot.Callback == 0x629F90));
        NativeBattleModuleDraw shadow = Assert.Single(step.Draws.Where(draw => draw.Resource!.Address == 0xDEDDD8));
        Assert.Equal(new NativeBattleModuleAnchor(100, 25, 300, 0), shadow.Anchor);
        Assert.Equal(0x717171u, shadow.SpriteColor);
        Assert.Equal((ushort)0xC, shadow.SpriteFlags);
        Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(1024, 0, 0)).ToArray(), shadow.SpriteWorldRotation.ToArray());
    }

    [Theory]
    [InlineData(0x62A0A0u)]
    [InlineData(0x629F90u)]
    public void ParticleSignedWordMotionUsesNativeFrictionAccelerationAndPauseSemantics(uint callback)
    {
        var fixture = Fixture(1);
        NativeBattleEffectTaskPool children = fixture.Effect.Pools[1];
        NativeBattleModulePoolTask task = NativeBattleTaskFixture.EditTask(children.Slots[0], taskData => taskData with { Callback = callback });
        task = task.WriteWord(12, callback == 0x629F90 ? (ushort)7 : (ushort)0).WriteWord(14, 0).WriteWord(16, 100).WriteWord(18, 200).WriteWord(20, 300).WriteWord(22, unchecked((ushort)-33)).WriteWord(24, unchecked((ushort)-17)).WriteWord(26, unchecked((ushort)-33)).WriteWord(28, callback == 0x629F90 ? (ushort)17 : (ushort)4096).WriteWord(32, 3000);
        NativeBattleOriginalEffectState effect = fixture.Effect with { Pools = fixture.Effect.Pools.SetItem(1, NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, task) })) };
        NativeBattleFireStep result = NativeBattleFireEffect.Step(effect, fixture.Inputs);
        NativeBattleModulePoolTask moved = result.Effect.Pools[1].Slots[0];
        Assert.Single(result.Draws);
        Assert.Equal(fixture.Inputs.CrtRandom, result.CrtRandomAfter);
        Assert.Equal((ushort)167, moved.ReadWord(18));
        if (callback == 0x62A0A0)
        {
            Assert.Equal(unchecked((ushort)-35), moved.ReadWord(22));
            Assert.Equal((ushort)4032, moved.ReadWord(28));
            Assert.Equal((ushort)1, moved.ReadWord(12));
            Assert.Equal(-512, result.Draws[0].DepthOffset);
        }
        else
        {
            Assert.Equal((ushort)83, moved.ReadWord(16));
            Assert.Equal((ushort)317, moved.ReadWord(20));
            Assert.Equal(unchecked((ushort)-15), moved.ReadWord(24));
            Assert.Equal(unchecked((ushort)-36), moved.ReadWord(26));
            Assert.Equal((ushort)16, moved.ReadWord(28));
            Assert.Equal((ushort)2936, moved.ReadWord(32));
            Assert.Equal((ushort)6, moved.ReadWord(12));
            Assert.Equal((ushort)2, result.Draws[0].Phase);
        }
    }

    private static (NativeBattleOriginalEffectState Effect, NativeBattleFireInputs Inputs) Fixture(int targets)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(3, 2, 11, -1, 1, 2, null));
        for (int index = 0; index < targets; index++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)(4 + index), 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish((byte)targets);
        NativeBattleOriginalEffectState effect = new(2, 1, new(2, 0x6298A0, 0x629880), new(3, 0, 0x30, 0, 0, 0), null);
        effect = NativeBattleFireEffect.InitializePools(effect, packets);
        ImmutableArray<NativeBattleModuleResource> resources = [Sprite(0xDEDC2C, 0), Sprite(0xDEDDD8, 0), Sprite(0xDEDF84, 0), Sprite(0x123450, 5)];
        NativeBattleFireInputs inputs = new(0, packets, NativeCrtRandomState.FromColdProcess(), model => new((short)(model * 100), -600, 200, 2000), _ => new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), resources);
        return (effect, inputs);
    }

    private static NativeBattleModuleResource Sprite(uint address, byte id)
    {
        byte[] bytes = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int frame = 0; frame <= 16; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), 44);
        }
        NativeBattleModuleResource resource = new(id, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
        resource.Validate();
        return resource;
    }
}
