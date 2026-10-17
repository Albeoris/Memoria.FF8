using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpectralCastingModuleTests
{
    [Theory]
    [InlineData(2, 0x3B27)]
    [InlineData(3, 0x3C67)]
    public void Wrapper_runs_under_pause_and_writes_only_native_child_fields(int selector, ushort material)
    {
        NativeBattleModuleState original = NativeBattleModuleState.Clear();
        NativeBattleModuleTask retained = original.Slots[1] with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() };
        original = original with { Slots = original.Slots.SetItem(1, retained) };
        NativeBattleModuleStep step = original.AppendModelEffect(4, (byte)selector).Advance(new(1));
        NativeBattleModuleTask child = Assert.Single(step.State.Slots, task => task.Flags != 0);
        Assert.Equal(NativeBattleModuleKind.SpectralCasting573800, child.Kind);
        Assert.Equal((short)0, Word(child, 0));
        Assert.Equal((short)0, Word(child, 2));
        Assert.Equal(material, unchecked((ushort)Word(child, 16)));
        Assert.Equal(0x1D972C0u + 4 * 156u, BinaryPrimitives.ReadUInt32LittleEndian(child.Payload.AsSpan().Slice(20)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 12), child.Payload.Skip(4).Take(12));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, child.Payload.AsSpan().Slice(18, 2).ToArray());
        Assert.Empty(step.Draws);
        Assert.Null(step.CrtRandomAfter);
    }

    [Theory]
    [InlineData(2, 0x3B27)]
    [InlineData(3, 0x3C67)]
    public void Controller_keeps_sound_CRT_child_order_and_same_pass_draws(int selector, ushort material)
    {
        NativeCrtRandomState random = new(7);
        NativeBattleModuleStep first = NativeBattleModuleState.Clear().AppendModelEffect(0, (byte)selector).Advance(Inputs(random));
        Assert.Equal(new ushort[] { 22, 23 }, first.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).Select(draw => draw.Sound));
        Assert.Equal(21, first.State.PublishedCount);
        Assert.Equal(Advance(random, 120), first.CrtRandomAfter);
        Assert.Single(first.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Sprite);
        NativeBattleModuleStep second = first.State.Advance(Inputs(first.CrtRandomAfter!));
        Assert.Equal(47, second.State.PublishedCount);
        Assert.Equal(Advance(random, 250), second.CrtRandomAfter);
        NativeBattleModuleKind[] children = Walk(second.State).Skip(21).Take(6).Select(task => task.Kind).ToArray();
        Assert.Equal(new[] { NativeBattleModuleKind.SpectralCastingColumn573F40, NativeBattleModuleKind.SpectralCastingRing5740D0, NativeBattleModuleKind.SpectralCastingRing5740D0, NativeBattleModuleKind.SpectralCastingRing5740D0, NativeBattleModuleKind.SpectralCastingRing5740D0, NativeBattleModuleKind.SpectralCastingGround573DF0 }, children);
        Assert.Equal(new short[] { 0, 3, 7, 11 }, Walk(second.State).Where(task => task.Kind == NativeBattleModuleKind.SpectralCastingRing5740D0).Select(task => Word(task, 2)));
        Assert.All(Walk(second.State).Where(task => task.Kind == NativeBattleModuleKind.SpectralCastingRing5740D0), task => Assert.Equal(material, unchecked((ushort)Word(task, 10))));
        Assert.Equal(new byte[] { 2, 3, 7 }, second.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Mesh).Select(draw => draw.MeshId));
        Assert.Equal(3, second.Draws.Count(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Complete_family_restores_each_pool_step_and_consumes_exactly370_CRT_draws(int selector)
    {
        NativeCrtRandomState initial = new(17);
        NativeCrtRandomState random = initial;
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, (byte)selector);
        int callbacks = 0;
        while (state.Head is not null && callbacks < 64)
        {
            NativeBattleModuleState clone = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            NativeBattleModuleStep first = state.Advance(Inputs(random));
            NativeBattleModuleStep independent = clone.Advance(Inputs(random));
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(independent));
            state = first.State;
            random = first.CrtRandomAfter!;
            callbacks++;
        }
        Assert.Equal(31, callbacks);
        Assert.Null(state.Head);
        Assert.Equal(0, state.PublishedCount);
        Assert.Equal(Advance(initial, 370), random);
    }

    [Theory]
    [InlineData(NativeBattleModuleKind.SpectralCastingRing5740D0, false)]
    [InlineData(NativeBattleModuleKind.SpectralCastingRing5740D0, true)]
    [InlineData(NativeBattleModuleKind.SpectralCastingParticle573C40, false)]
    [InlineData(NativeBattleModuleKind.SpectralCastingParticle573C40, true)]
    public void Positive_signed_delay_returns_before_draw_or_resource_reads(NativeBattleModuleKind kind, bool paused)
    {
        byte[] payload = new byte[24];
        Put(payload, 2, 1);
        NativeBattleModuleStep step = Single(kind, payload).Advance(new(paused ? 1u : 0));
        Assert.Empty(step.Draws);
        Assert.Equal(paused ? (short)1 : (short)0, Word(step.State.Slots[0], 2));
        Assert.Equal((short)0, Word(step.State.Slots[0], 0));
    }

    [Fact]
    public void Sprite_draw_precedes_pause_and_final_life_release_with_native_frame_wrap_and_palette()
    {
        byte[] payload = new byte[24];
        Put(payload, 0, 1);
        Put(payload, 4, 100);
        Put(payload, 6, 200);
        Put(payload, 8, 300);
        Put(payload, 10, 10);
        Put(payload, 12, 7);
        Put(payload, 14, -8);
        Put(payload, 16, 9);
        Put(payload, 18, 10);
        Put(payload, 20, 6400);
        Put(payload, 22, 640);
        NativeBattleModuleState state = Single(NativeBattleModuleKind.SpectralCastingParticle573C40, payload);
        NativeBattleModuleStep paused = state.Advance(Inputs(new(7)) with { GraphicsFlags = 1 });
        NativeBattleModuleDraw draw = Assert.Single(paused.Draws);
        Assert.Equal((ushort)10, draw.Phase);
        Assert.Equal((ushort)0x210, draw.SpriteFlags);
        Assert.Equal(new byte[] { 4, 0, 0, 0 }, draw.SpritePaletteOffsets);
        Assert.Equal(new NativeBattleModuleAnchor(100, 200, 300, 10), draw.Anchor);
        Assert.Equal(payload, paused.State.Slots[0].Payload);
        NativeBattleModuleStep released = state.Advance(Inputs(new(7)));
        Assert.Equal(JsonSerializer.Serialize(draw), JsonSerializer.Serialize(Assert.Single(released.Draws)));
        Assert.Equal((short)107, Word(released.State.Slots[0], 4));
        Assert.Equal((short)192, Word(released.State.Slots[0], 6));
        Assert.Equal((short)309, Word(released.State.Slots[0], 8));
        Assert.Equal((short)0, Word(released.State.Slots[0], 10));
        Assert.Null(released.State.Head);
    }

    [Theory]
    [InlineData(NativeBattleModuleKind.SpectralCastingGround573DF0, 14, 7, 4088)]
    [InlineData(NativeBattleModuleKind.SpectralCastingColumn573F40, 26, 2, 4096)]
    [InlineData(NativeBattleModuleKind.SpectralCastingRing5740D0, 14, 3, 4090)]
    public void Mesh_final_frame_draws_before_pause_or_release(NativeBattleModuleKind kind, short phase, byte mesh, int fade)
    {
        byte[] payload = new byte[24];
        Put(payload, 0, phase);
        Put(payload, 10, 0x3C67);
        Put(payload, 14, -5);
        Put(payload, 16, 32760);
        Put(payload, 18, 16);
        Put(payload, 20, 20);
        Put(payload, 22, -7);
        NativeBattleModuleState state = Single(kind, payload);
        NativeBattleModuleStep paused = state.Advance(new(1));
        NativeBattleModuleDraw draw = Assert.Single(paused.Draws);
        Assert.Equal(mesh, draw.MeshId);
        Assert.Equal(fade, draw.MeshFade);
        Assert.Equal(payload, paused.State.Slots[0].Payload);
        NativeBattleModuleStep released = state.Advance(new(0));
        Assert.Equal(JsonSerializer.Serialize(draw), JsonSerializer.Serialize(Assert.Single(released.Draws)));
        Assert.Null(released.State.Head);
        Assert.Equal(unchecked((short)(32760 + 16)), Word(released.State.Slots[0], 16));
        if (kind == NativeBattleModuleKind.SpectralCastingColumn573F40)
        {
            Assert.Equal((short)1, Word(released.State.Slots[0], 14));
            Assert.Equal((short)-6, Word(released.State.Slots[0], 22));
        }
    }

    [Theory]
    [InlineData(-4096, -9011, -18022, -4505)]
    [InlineData(32767, 32767, -2, 0)]
    public void Seed_scale_clamps_only_the_upper_bound_and_narrows_ground_before_division(short scale, short growth, short ground, short groundGrowth)
    {
        byte[] payload = new byte[24];
        Put(payload, 0, 1);
        Put(payload, 16, 0x3B27);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20), 0x1D972C0);
        NativeBattleModuleInputs inputs = Inputs(new(7)) with { ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 700, scale) };
        NativeBattleModuleStep step = Single(NativeBattleModuleKind.SpectralCasting573800, payload).Advance(inputs);
        NativeBattleModuleDraw column = Assert.Single(step.Draws, draw => draw.MeshId == 2 && draw.Kind == NativeBattleModuleDrawKind.Mesh);
        Assert.Equal((short)0, column.MeshScale!.Value.X);
        NativeBattleModuleTask columnTask = Assert.Single(step.State.Slots, task => task.Flags != 0 && task.Kind == NativeBattleModuleKind.SpectralCastingColumn573F40);
        Assert.Equal(growth, Word(columnTask, 16));
        NativeBattleModuleDraw groundDraw = Assert.Single(step.Draws, draw => draw.MeshId == 7 && draw.Kind == NativeBattleModuleDrawKind.Mesh);
        Assert.Equal(ground, groundDraw.MeshScale!.Value.X);
        NativeBattleModuleTask groundTask = Assert.Single(step.State.Slots, task => task.Flags != 0 && task.Kind == NativeBattleModuleKind.SpectralCastingGround573DF0);
        Assert.Equal(unchecked((short)(groundGrowth - groundGrowth / 8)), Word(groundTask, 18));
    }

    [Fact]
    public void Missing_reached_owners_and_native_zero_divisor_remain_strict()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, 2);
        Assert.Throws<NotSupportedException>(() => state.Advance(new(0)));
        Assert.Throws<NotSupportedException>(() => state.Advance(Inputs(new(7)) with { CrtRandom = null }));
        NativeBattleModuleInputs zero = Inputs(new(7)) with { ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 0, 0) };
        NativeBattleModuleStep first = state.Advance(zero);
        Assert.Throws<InvalidDataException>(() => first.State.Advance(zero with { CrtRandom = first.CrtRandomAfter }));
    }

    private static NativeBattleModuleInputs Inputs(NativeCrtRandomState random) => new(0) { CrtRandom = random, ReadModelAnchor = _ => new(100, 200, 300, 0), ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 700, 4096), Resources = [Sprite()] };

    private static NativeCrtRandomState Advance(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }

    private static IEnumerable<NativeBattleModuleTask> Walk(NativeBattleModuleState state)
    {
        for (int? next = state.Head; next is int index; next = state.Slots[index].Next)
        {
            yield return state.Slots[index];
        }
    }

    private static NativeBattleModuleState Single(NativeBattleModuleKind kind, byte[] payload)
    {
        (NativeBattleModuleState state, int slot) = NativeBattleModuleState.Clear().Append(kind);
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
    }

    private static short Word(NativeBattleModuleTask task, int offset) => BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(offset));
    private static void Put(byte[] payload, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset), unchecked((short)value));

    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[298];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 11);
        for (int frame = 0; frame < 11; frame++)
        {
            int offset = 34 + frame * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 8;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 8;
            bytes[offset + 7] = 0x2E;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), ushort.MaxValue);
        return new(6, 0xC79344, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
