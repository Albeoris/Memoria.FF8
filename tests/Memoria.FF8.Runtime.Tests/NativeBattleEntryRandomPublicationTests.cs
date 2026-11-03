using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEntryRandomPublicationTests
{
    [Fact]
    public void Accepted_entry_publishes_the_single_CRT_draw_once_and_preserves_it_in_JSON()
    {
        GameSession session = PendingSession();
        NativeCrtRandomState before = session.State.CrtRandom!;
        NativeCrtRandomState after = before.Draw(out _);
        BattleSession battle = Battle(after);

        session.SetLiveBattle(battle, before);

        Assert.Equal(after, session.State.CrtRandom);
        Assert.True(session.HasActiveBattle);
        Assert.NotNull(session.LiveBattle);
        GameState published = session.State;
        Assert.Throws<InvalidOperationException>(() => session.SetLiveBattle(battle, before));
        Assert.Same(published, session.State);
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.PrepareSnapshot()))!;
        Assert.Equal(after, restored.CrtRandom);
        Assert.Equal(after, restored.Battle!.Definition.NativeCrtRandom);
        Assert.Equal(battle.Frame, restored.Battle.Frame);
        Assert.Equal(after.Draw(out _), restored.CrtRandom!.Draw(out _));
    }

    [Fact]
    public void Rejected_entry_leaves_the_CRT_owner_and_live_binding_untouched()
    {
        GameSession session = PendingSession();
        NativeCrtRandomState before = session.State.CrtRandom!;
        GameState source = session.State;

        Assert.Throws<InvalidOperationException>(() => session.SetLiveBattle(Battle(before), before));
        Assert.Same(source, session.State);
        Assert.Null(session.LiveBattle);
        Assert.Throws<InvalidOperationException>(() => session.SetLiveBattle(null, before));
        Assert.Same(source, session.State);
        NativeCrtRandomState after = before.Draw(out _);
        Assert.Throws<InvalidOperationException>(() => session.SetLiveBattle(Battle(after, 42), before));
        Assert.Same(source, session.State);
        Assert.Throws<InvalidOperationException>(() => session.SetLiveBattle(Battle(after), new NativeCrtRandomState(99)));
        Assert.Same(source, session.State);
        Assert.Throws<InvalidDataException>(() => session.SetLiveBattle(Battle(after), before, MagicSystemFixture.Create() with { Version = 0 }));
        Assert.Same(source, session.State);
        Assert.Null(session.LiveBattle);
    }

    [Fact]
    public void Explicit_seed_publication_does_not_consume_a_CRT_draw()
    {
        GameSession session = PendingSession();
        NativeCrtRandomState before = session.State.CrtRandom!;

        session.SetLiveBattle(Battle(before));

        Assert.Equal(before, session.State.CrtRandom);
    }

    private static GameSession PendingSession()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.SetPendingBattleEncounter(new("entry", 512, new string('A', 64), SceneKind.Field, session.State.FieldId, 0, new string('B', 64), null));
        return session;
    }

    private static BattleSession Battle(NativeCrtRandomState random, byte seed = 41)
    {
        return new(new BattleDefinition
        {
            NativeCrtRandom = random,
            NativeSeed = seed,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }
            ]
        }, BattleAtbMode.Normal);
    }
}
