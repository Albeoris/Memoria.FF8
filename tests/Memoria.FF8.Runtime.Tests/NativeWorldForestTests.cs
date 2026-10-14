using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldForestTests
{
    private static readonly NativeWorldForestEffect[] Effects = Enumerable.Range(4, 6).Select(type => new NativeWorldForestEffect((byte)type, 3968, 8, 4, 2, 255)).ToArray();

    [Fact]
    public void Emission_uses_previous_raw_speed_and_increments_random_before_lookup()
    {
        byte[] random = Enumerable.Range(0, 256).Select(index => (byte)index).ToArray();
        NativeWorldForestState state = NativeWorldForestState.Create(255, -5);
        NativeWorldForestState first = state.Advance(1, 1000, -800, -2000, 2048, 0, random, Effects);
        Assert.Empty(first.Particles);
        Assert.Equal((byte)0, first.RandomCursor);
        Assert.Equal((byte)12, first.RandomOffset);
        NativeWorldForestState second = first.Advance(1, 1032, -800, -2000, 2048, 32, random, Effects);
        NativeWorldForestParticle particle = Assert.Single(second.Particles);
        Assert.Equal((1032, -1120, 2000, (byte)5, (byte)1), (particle.X, particle.NativeY, particle.GraphicsZ, particle.Type, particle.Age));
        Assert.Equal((byte)5, second.RandomCursor);
        Assert.True(particle.Visible);
        Assert.Equal((ushort)((4096 + unchecked((byte)(4 - 12)) - 128) * 3968 / 4096), particle.Scale);
        Assert.Equal((byte)(8 + (unchecked((byte)(5 - 12)) & 7) - 4), particle.Lifetime);
    }

    [Fact]
    public void Pool_reuses_first_expired_slot_and_restored_next_callback_matches()
    {
        byte[] random = new byte[256];
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0) with { Particles = [new(0, 4, 0, 0, 0, 0, 0, 4096, 4, 4, true), new(1, 4, 0, 0, 0, 0, 0, 4096, 3, 4, true)] };
        NativeWorldForestState first = state.Advance(0, 200, -300, 400, 0, 32, random, Effects);
        Assert.Equal(2, first.Particles.Length);
        Assert.Equal((byte)1, first.Particles[0].Age);
        Assert.Equal((byte)4, first.Particles[1].Age);
        Assert.True(first.Particles[1].Visible);
        NativeWorldForestState restored = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(first))!;
        NativeWorldForestState next = first.Advance(6, 300, -300, 400, 0, 0, random, Effects);
        Assert.False(next.Particles[1].Visible);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restored.Advance(6, 300, -300, 400, 0, 0, random, Effects)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(first).Replace("RandomCursor", "MissingCursor", StringComparison.Ordinal)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(first).Replace("Scale", "MissingScale", StringComparison.Ordinal)));
    }

    [Fact]
    public void CanonicalPriorGraphicsInputIsRequiredEachCallbackWithoutAStoredForestAlias()
    {
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0);
        byte[] random = new byte[256];
        Assert.Empty(state.Advance(0, 0, 0, 0, 0, 0, random, Effects).Particles);
        Assert.Single(state.Advance(0, 0, 0, 0, 0, 1, random, Effects).Particles);
        Assert.DoesNotContain("RawSpeed", JsonSerializer.Serialize(state));
        string before = JsonSerializer.Serialize(state);
        Assert.Throws<InvalidDataException>(() => state.Advance(0, 0, 0, 0, 0, -1, random, Effects));
        Assert.Throws<InvalidDataException>(() => state.Advance(0, 0, 0, 0, 0, 46341, random, Effects));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }
}
