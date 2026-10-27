using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianCastingModuleTests
{
    [Fact]
    public void Guardian_casting_uses_its_own_sound_pair_and_same_pass_particle_trails()
    {
        NativeCrtRandomState random = new(7);
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, 1);
        NativeBattleModuleStep paused = state.Advance(Inputs(random) with { GraphicsFlags = 1 });
        // 508420 publishes the surviving list count even when 5742A0 itself is paused.
        Assert.Equal(JsonSerializer.Serialize(state with { PublishedCount = 1 }), JsonSerializer.Serialize(paused.State));
        Assert.Empty(paused.Draws);
        Assert.Equal(random, paused.CrtRandomAfter);
        NativeBattleModuleStep first = state.Advance(Inputs(random));
        Assert.Equal(new ushort[] { 7, 8 }, first.Draws.Select(draw => draw.Sound));
        Assert.Equal(random, first.CrtRandomAfter);
        NativeBattleModuleStep second = first.State.Advance(Inputs(random));
        Assert.Equal(23, second.State.PublishedCount);
        Assert.Equal(5, second.State.Slots.Count(task => task.Kind == NativeBattleModuleKind.GuardianCastingParticle574670 && task.Flags != 0));
        Assert.Equal(15, second.State.Slots.Count(task => task.Kind == NativeBattleModuleKind.GuardianCastingTrail574580 && task.Flags != 0));
        Assert.Equal(20, second.Draws.Count(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite));
        Assert.Contains(second.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Mesh && draw.MeshId == 4 && draw.MeshClut == 0x3867);
        Assert.Contains(second.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Mesh && draw.MeshId == 7);
        Assert.Equal(Advance(random, 8), second.CrtRandomAfter);
        foreach (NativeBattleModuleTask trail in second.State.Slots.Where(task => task.Kind == NativeBattleModuleKind.GuardianCastingTrail574580 && task.Flags != 0))
        {
            Assert.InRange(BinaryPrimitives.ReadInt16LittleEndian(trail.Payload.AsSpan().Slice(10)), (short)0, (short)6);
        }
    }

    [Fact]
    public void Guardian_casting_restores_exact_payloads_and_uses_all26_original_CRT_draws()
    {
        NativeCrtRandomState initial = new(17);
        NativeCrtRandomState random = initial;
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, 1);
        int callbacks = 0;
        while (state.Head is not null && callbacks < 50)
        {
            NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            NativeBattleModuleStep step = state.Advance(Inputs(random));
            NativeBattleModuleStep resumed = restored.Advance(Inputs(random));
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(resumed));
            state = step.State;
            random = step.CrtRandomAfter!;
            callbacks++;
        }
        Assert.Equal(38, callbacks);
        Assert.Equal(0, state.PublishedCount);
        Assert.Null(state.Head);
        Assert.Equal(Advance(initial, 26), random);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Orbit_sprite_uses_the_predicted_point_while_trails_keep_the_current_point_even_when_paused(bool paused)
    {
        byte[] payload = new byte[24];
        Word(payload, 4, 100);
        Word(payload, 6, 200);
        Word(payload, 8, 300);
        Word(payload, 10, -38);
        Word(payload, 22, 256);
        NativeBattleModuleState state = SingleTask(NativeBattleModuleKind.GuardianCastingParticle574670, payload);
        NativeCrtRandomState random = new(7);

        NativeBattleModuleStep step = state.Advance(Inputs(random) with { GraphicsFlags = paused ? 1u : 0 });

        NativeBattleModuleDraw sprite = Assert.Single(step.Draws);
        NativeBattleVector predicted = NativeBattlePoseMatrices.RotateVector(new(0, 120, 0), new(0, 0, -256));
        Assert.Equal(new NativeBattleModuleAnchor(unchecked((short)(predicted.X + 100)), 162, unchecked((short)(predicted.Z + 300)), 0), sprite.Anchor);
        NativeBattleModuleTask particle = step.State.Slots[0];
        Assert.Equal((short)100, Read(particle, 12));
        Assert.Equal((short)200, Read(particle, 14));
        Assert.Equal((short)44, Read(particle, 16));
        Assert.Equal((short)6, Read(particle, 18));
        Assert.Equal((ushort)7, sprite.Phase);
        Assert.Equal((ushort)4, sprite.SpriteFlags);
        Assert.Equal(0u, sprite.SpriteColor);
        Assert.Equal(paused ? (short)200 : (short)162, Read(particle, 6));
        Assert.Equal(paused ? (short)-38 : (short)-37, Read(particle, 10));
        Assert.Equal(paused ? (short)0 : (short)120, Read(particle, 20));
        Assert.Equal(paused ? (short)0 : (short)1, Read(particle, 0));
        Assert.Equal(random, step.CrtRandomAfter);
    }

    [Theory]
    [InlineData(false, -32760, 32767, -32536, -32744, 32758, -31536)]
    [InlineData(true, 32760, -32760, 32000, -32760, 32767, -32536)]
    public void Released_particle_predicts_another_velocity_step_with_signed_WORD_wrap_and_paused_frame_cache(bool paused, short cacheX, short cacheY, short cacheZ, short drawnX, short drawnY, short drawnZ)
    {
        byte[] payload = new byte[24];
        Word(payload, 0, 23);
        Word(payload, 2, 5);
        Word(payload, 4, 16);
        Word(payload, 6, -9);
        Word(payload, 8, 1000);
        Word(payload, 12, 32760);
        Word(payload, 14, -32760);
        Word(payload, 16, 32000);
        Word(payload, 18, -1);
        NativeCrtRandomState random = new(7);
        NativeBattleModuleStep step = SingleTask(NativeBattleModuleKind.GuardianCastingParticle574670, payload).Advance(Inputs(random) with { GraphicsFlags = paused ? 1u : 0 });

        NativeBattleModuleDraw sprite = Assert.Single(step.Draws);
        Assert.Equal(new NativeBattleModuleAnchor(drawnX, drawnY, drawnZ, 0), sprite.Anchor);
        NativeBattleModuleTask particle = step.State.Slots[0];
        Assert.Equal(cacheX, Read(particle, 12));
        Assert.Equal(cacheY, Read(particle, 14));
        Assert.Equal(cacheZ, Read(particle, 16));
        Assert.Equal((short)1, Read(particle, 18));
        Assert.Equal((ushort)2, sprite.Phase);
        Assert.Equal(0x707070u, sprite.SpriteColor);
        Assert.Equal(paused ? (short)16 : (short)18, Read(particle, 4));
        Assert.Equal(paused ? (short)-9 : (short)-11, Read(particle, 6));
        Assert.Equal(paused ? (short)1000 : (short)1125, Read(particle, 8));
        Assert.Equal(paused ? (short)23 : (short)24, Read(particle, 0));
        Assert.Equal(paused ? (short)5 : (short)6, Read(particle, 2));
        Assert.Equal(random, step.CrtRandomAfter);
    }

    [Fact]
    public void Trail_waits_for_signed_parent_frame_then_copies_retained_position_and_frame_even_from_a_released_slot()
    {
        byte[] payload = new byte[24];
        Word(payload, 0, 27);
        Word(payload, 2, 2);
        Word(payload, 4, 10);
        Word(payload, 6, 20);
        Word(payload, 8, 30);
        Word(payload, 10, -1);
        Word(payload, 20, 1);
        NativeBattleModuleState state = SingleTask(NativeBattleModuleKind.GuardianCastingTrail574580, payload);
        byte[] parentPayload = new byte[24];
        Word(parentPayload, 12, 100);
        Word(parentPayload, 14, 200);
        Word(parentPayload, 16, 300);
        Word(parentPayload, 18, -1);
        state = state with { Slots = state.Slots.SetItem(1, state.Slots[1] with { Payload = parentPayload.ToImmutableArray() }) };
        NativeBattleModuleInputs inputs = Inputs(new(7));
        NativeBattleModuleStep waiting = state.Advance(inputs);
        Assert.Empty(waiting.Draws);
        Assert.Equal((short)27, Read(waiting.State.Slots[0], 0));

        Word(parentPayload, 18, 3);
        state = state with { Slots = state.Slots.SetItem(1, state.Slots[1] with { Payload = parentPayload.ToImmutableArray() }) };
        NativeBattleModuleStep paused = state.Advance(inputs with { GraphicsFlags = 1 });
        NativeBattleModuleDraw sprite = Assert.Single(paused.Draws);
        Assert.Equal(new NativeBattleModuleAnchor(100, 200, 300, 0), sprite.Anchor);
        Assert.Equal((ushort)3, sprite.Phase);
        Assert.Equal(0x181818u, sprite.SpriteColor);
        NativeBattleModuleTask trail = paused.State.Slots[0];
        Assert.Equal((short)27, Read(trail, 0));
        Assert.Equal((short)100, Read(trail, 4));
        Assert.Equal((short)3, Read(trail, 10));
        Assert.Equal((short)10, Read(trail, 12));
        Assert.Equal((short)-1, Read(trail, 18));
        NativeBattleModuleStep retained = paused.State.Advance(inputs);
        Assert.Equal(1, retained.State.PublishedCount);
        NativeBattleModuleStep released = retained.State.Advance(inputs);
        Assert.Equal(0, released.State.PublishedCount);
        Assert.Null(released.State.Head);
    }

    private static NativeCrtRandomState Advance(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }

    private static NativeBattleModuleState SingleTask(NativeBattleModuleKind kind, byte[] payload)
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        return state with { Head = 0, Tail = 0, Slots = state.Slots.SetItem(0, new(1, 0, null, kind, payload.ToImmutableArray())) };
    }

    private static short Read(NativeBattleModuleTask task, int offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(offset));

    private static void Word(byte[] payload, int offset, int value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset), unchecked((short)value));
    }

    private static NativeBattleModuleInputs Inputs(NativeCrtRandomState random)
    {
        byte[] bytes = new byte[52];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 8);
        // Empty quads isolate callback and payload contracts; installed-resource rendering has a separate check.
        for (int phase = 0; phase < 8; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), 28);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), ushort.MaxValue);
        return new(0) { CrtRandom = random, ReadModelAnchor = _ => new(10, 20, 30, 40), ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 123, 4096), Resources = [new(4, 1, 0x570CC0, 0x570BC0, bytes.ToImmutableArray())] };
    }
}
