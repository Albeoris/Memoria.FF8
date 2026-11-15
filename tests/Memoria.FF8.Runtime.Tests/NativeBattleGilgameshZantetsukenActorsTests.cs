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
        actors = actors.WithRow(3, NativeBattleGilgameshZantetsukenActors.WithMatrix(NativeBattleGilgameshZantetsukenActors.WithWord(actors.ReadRow(3), 0, -123), matrix));
        Assert.Equal((short)-123, NativeBattleGilgameshZantetsukenActors.ReadWord(actors.ReadRow(3), 0));
        Assert.Equal(unchecked((short)0xA5A5), NativeBattleGilgameshZantetsukenActors.ReadWord(actors.ReadRow(3), 6));
        Assert.Equal(unchecked((short)0xA5A5), NativeBattleGilgameshZantetsukenActors.ReadWord(actors.ReadRow(2), 0));
        Assert.Equal(unchecked((short)0xA5A5), NativeBattleGilgameshZantetsukenActors.ReadWord(actors.ReadRow(3), 58));
        Assert.False(NativeBattleGilgameshZantetsukenActors.ReadMatrix(actors.ReadRow(3)).Known[18]);
        Assert.False(NativeBattleGilgameshZantetsukenActors.ReadMatrix(actors.ReadRow(3)).Known[19]);
        Assert.Equal(NativeBattleTestSnapshots.Json(matrix.ReadMatrix()), NativeBattleTestSnapshots.Json(NativeBattleGilgameshZantetsukenActors.ReadMatrix(actors.ReadRow(3)).ReadMatrix()));
        NativeBattleGilgameshZantetsukenActors restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGilgameshZantetsukenActors>(NativeBattleTestSnapshots.Json(actors))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(actors), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Unknown_storage_only_becomes_known_at_actual_stores_and_is_bounded()
    {
        NativeBattleGilgameshZantetsukenActors actors = NativeBattleGilgameshZantetsukenActors.FromUnknown();
        actors = actors.WithRow(6, NativeBattleGilgameshZantetsukenActors.WithWord(actors.ReadRow(6), 4, 0));
        Assert.Equal((short)0, NativeBattleGilgameshZantetsukenActors.ReadWord(actors.ReadRow(6), 4));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshZantetsukenActors.ReadWord(actors.ReadRow(6), 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshZantetsukenActors.ReadMatrix(actors.ReadRow(6)).ReadMatrix());
        Assert.Throws<InvalidDataException>(() => actors.WithRow(7, NativeBattleEffectArena.Clear()));
        Assert.Throws<InvalidDataException>(() => new NativeBattleGilgameshZantetsukenActors(NativeBattleEffectArena.Clear().Write(6 * NativeBattleGilgameshZantetsukenActors.RecordStride + 59, new byte[2])).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattleGilgameshZantetsukenActors(NativeBattleEffectArena.Clear().Write(420, new byte[1])).Validate());
    }
}
