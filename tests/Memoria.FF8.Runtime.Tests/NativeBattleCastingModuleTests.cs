using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCastingModuleTests
{
    [Fact]
    public void A5_override_selects_original_table_entry1_and_clears_only_its_graphics_bit()
    {
        Assert.Equal(((byte)1, 0x80000010u), NativeBattleModuleState.SelectModelEffect(255, 0xC0000010));
        Assert.Equal(((byte)255, 0x80000010u), NativeBattleModuleState.SelectModelEffect(255, 0x80000010));
        Assert.Throws<NotSupportedException>(() => NativeBattleModuleState.Clear().AppendModelEffect(0, 6));
    }

    [Fact]
    public void Original574C80_appends_all_twelve_children_in_the_same_pass_and_consumes35_crt_draws()
    {
        NativeCrtRandomState initial = new(7);
        NativeBattleModuleStep first = NativeBattleModuleState.Clear().AppendModelEffect(0, 0).Advance(Inputs(initial));
        Assert.Equal(13, first.State.PublishedCount);
        Assert.Equal(5, first.State.Slots.Count(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CastingRing5753A0));
        Assert.Equal(6, first.State.Slots.Count(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CastingParticle574F50));
        Assert.Equal(new ushort[] { 5, 6 }, first.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).Select(draw => draw.Sound));
        Assert.Contains(first.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Mesh && draw.MeshId == 1 && draw.MeshClut == 0x3867 && draw.MeshScale!.Value.Y == 0);
        Assert.Contains(first.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Mesh && draw.MeshId == 7 && draw.Anchor!.Y == 123);
        NativeCrtRandomState expected = initial;
        for (int draw = 0; draw < 35; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, first.CrtRandomAfter);
        NativeBattleModuleStep paused = first.State.Advance(Inputs(expected) with { GraphicsFlags = 1 });
        Assert.Equal(JsonSerializer.Serialize(first.State), JsonSerializer.Serialize(paused.State));
        Assert.Equal(expected, paused.CrtRandomAfter);
        Assert.DoesNotContain(paused.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Sound);
        Assert.Contains(paused.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Mesh);
    }

    [Fact]
    public void Casting_children_restore_raw_payloads_and_release_by_their_original_counter_contracts()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, 0);
        NativeCrtRandomState random = new(17);
        int callbacks = 0;
        while (state.Head is not null && callbacks < 40)
        {
            NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            NativeBattleModuleInputs inputs = Inputs(random);
            NativeBattleModuleStep step = state.Advance(inputs);
            NativeBattleModuleStep repeated = restored.Advance(inputs);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(repeated));
            state = step.State;
            random = step.CrtRandomAfter!;
            callbacks++;
        }
        Assert.Equal(31, callbacks);
        Assert.Null(state.Head);
        Assert.Equal(0, state.PublishedCount);
        NativeCrtRandomState expected = new(17);
        for (int draw = 0; draw < 1115; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
    }

    private static NativeBattleModuleInputs Inputs(NativeCrtRandomState random)
    {
        return new(0) { CrtRandom = random, ReadModelAnchor = _ => new(10, 20, 30, 40), ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 123, 4096), Resources = [Sprites()] };
    }

    private static NativeBattleModuleResource Sprites()
    {
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 10);
        for (int phase = 0; phase < 10; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(32 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), ushort.MaxValue);
        return new(5, 1, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
