using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshZantetsukenActorsTests
{
    [Fact]
    public void Matrix_knownness_and_unrelated_actor_bytes_survive_partial_writes_and_restore()
    {
        NativeBattleGilgameshZantetsukenActors actors = NativeBattleGilgameshZantetsukenActors.FromColdProcess(Enumerable.Repeat((byte)0xA5, 420).ToImmutableArray());
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 123, Y = -456, Z = 789 }, null);
        actors = actors.WithWord(3, 0, -123).WithMatrix(3, matrix);
        Assert.Equal((short)-123, actors.ReadWord(3, 0));
        Assert.Equal(unchecked((short)0xA5A5), actors.ReadWord(3, 6));
        Assert.Equal(unchecked((short)0xA5A5), actors.ReadWord(2, 0));
        Assert.Equal(unchecked((short)0xA5A5), actors.ReadWord(3, 58));
        Assert.False(actors.ReadMatrix(3).Known[18]);
        Assert.False(actors.ReadMatrix(3).Known[19]);
        Assert.Equal(JsonSerializer.Serialize(matrix.ReadMatrix()), JsonSerializer.Serialize(actors.ReadMatrix(3).ReadMatrix()));
        NativeBattleGilgameshZantetsukenActors restored = JsonSerializer.Deserialize<NativeBattleGilgameshZantetsukenActors>(JsonSerializer.Serialize(actors))!;
        Assert.Equal(JsonSerializer.Serialize(actors), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Unknown_storage_only_becomes_known_at_actual_stores_and_is_bounded()
    {
        NativeBattleGilgameshZantetsukenActors actors = NativeBattleGilgameshZantetsukenActors.FromUnknown().WithWord(6, 4, 0);
        Assert.Equal((short)0, actors.ReadWord(6, 4));
        Assert.Throws<NotSupportedException>(() => actors.ReadWord(6, 2));
        Assert.Throws<NotSupportedException>(() => actors.ReadMatrix(6).ReadMatrix());
        Assert.Throws<InvalidDataException>(() => actors.WithWord(7, 0, 1));
        Assert.Throws<InvalidDataException>(() => actors.WithWord(6, 59, 1));
        Assert.Throws<InvalidDataException>(() => new NativeBattleGilgameshZantetsukenActors(NativeBattleEffectArena.Clear().Write(420, new byte[1])).Validate());
    }
}
