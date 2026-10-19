using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeLimitGraphicsCallbackTests
{
    [Theory]
    [InlineData("Shot")]
    [InlineData("Duel")]
    [InlineData("Renzokuken")]
    public void Timed_input_between_effects_prevents_automatic_combat_time_fast_forward(string limit)
    {
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        PropertyInfo automaticAdvance = typeof(BattleSession).GetProperty("CanAutomaticallyAdvanceTurn", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True((bool)automaticAdvance.GetValue(session)!);

        switch (limit)
        {
            case "Shot":
                Field<Dictionary<string, BattleNativeShotState>>(session, "nativeShotProcesses").Add("party", new(0, 8, 800, 800, 2, BattleNativeShotPhase.Ready));
                break;
            case "Duel":
                Field<Dictionary<string, BattleNativeDuelState>>(session, "nativeDuelProcesses").Add("party", new(0, 8, 800, 800, BattleNativeDuelPhase.Ready));
                break;
            case "Renzokuken":
                Field<Dictionary<string, BattleNativeRenzokukenState>>(session, "nativeRenzokukenProcesses").Add("party", new(160, 8, null, new()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(limit));
        }

        Assert.Null(session.ActiveAction);
        Assert.Empty(session.QueuedActions);
        Assert.False((bool)automaticAdvance.GetValue(session)!);
        Assert.Equal(0, session.Frame);
    }

    [Theory]
    [InlineData(0x48D1A0u, BattleNativeShotPhase.Opening)]
    [InlineData(0x4AD7C0u, BattleNativeShotPhase.AwaitingEffect)]
    public void Shot_descriptor_readiness_preserves_its_running_timer_and_ammunition(uint callback, BattleNativeShotPhase phase)
    {
        BattleSession session = Fixture();
        Dictionary<string, BattleNativeShotState> shots = Field<Dictionary<string, BattleNativeShotState>>(session, "nativeShotProcesses");
        BattleNativeShotState before = new(0, 8, 160, 139, 9, phase) { UiSubframeTicks = 123 };
        shots.Add("party", before);
        NativeBattleGraphicsState graphics = Graphics();

        Assert.Same(graphics, Invoke(session, graphics, callback));
        Assert.Equal(before with { Phase = BattleNativeShotPhase.Ready }, shots["party"]);
        Assert.Equal(0, session.Frame);
        Assert.Empty(session.Events);
    }

    [Fact]
    public void A_late_Shot_readiness_callback_cannot_reopen_its_ending_widget()
    {
        BattleSession session = Fixture();
        Dictionary<string, BattleNativeShotState> shots = Field<Dictionary<string, BattleNativeShotState>>(session, "nativeShotProcesses");
        BattleNativeShotState ending = new(0, 8, 160, -1, 0, BattleNativeShotPhase.Ending);
        shots.Add("party", ending);

        Invoke(session, Graphics(), 0x4AD7C0);

        Assert.Equal(ending, shots["party"]);
    }

    [Theory]
    [InlineData(0x48E5A0u)]
    [InlineData(0x48E5E0u)]
    public void Duel_descriptor_refresh_clears_the_input_ring_without_restarting_its_timer(uint callback)
    {
        BattleSession session = Fixture();
        Dictionary<string, BattleNativeDuelState> duels = Field<Dictionary<string, BattleNativeDuelState>>(session, "nativeDuelProcesses");
        BattleNativeDuelState before = new(2, 8, 400, 279, BattleNativeDuelPhase.AwaitingEffect) { InputRing = [1, 2, 3, 4, 5, 6, 7, 8], InputCursor = 5, AutomaticCountdown = 30, PreviousButtons = 8, PendingPulse = 4, UiSubframeTicks = 91 };
        duels.Add("party", before);

        Invoke(session, Graphics(), callback);

        BattleNativeDuelState after = duels["party"];
        Assert.Equal(BattleNativeDuelPhase.Ready, after.Phase);
        Assert.All(after.InputRing, value => Assert.Equal((ushort)0, value));
        Assert.Equal(0, after.InputCursor);
        Assert.Equal(120, after.AutomaticCountdown);
        Assert.Equal((ushort)0, after.PendingPulse);
        Assert.Equal(before with { Phase = after.Phase, InputRing = after.InputRing, InputCursor = after.InputCursor, AutomaticCountdown = after.AutomaticCountdown, PendingPulse = after.PendingPulse }, after);
    }

    [Fact]
    public void Raw_action_acknowledgment_cannot_substitute_for_the_Shot_or_Duel_readiness_descriptor()
    {
        BattleSession session = Fixture();
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, Graphics());
        Dictionary<string, BattleNativeShotState> shots = Field<Dictionary<string, BattleNativeShotState>>(session, "nativeShotProcesses");
        Dictionary<string, BattleNativeDuelState> duels = Field<Dictionary<string, BattleNativeDuelState>>(session, "nativeDuelProcesses");
        BattleNativeShotState shot = new(0, 8, 160, 139, 9, BattleNativeShotPhase.AwaitingEffect);
        BattleNativeDuelState duel = new(2, 8, 400, 279, BattleNativeDuelPhase.AwaitingEffect) { InputRing = [1, 2, 3, 4, 5, 6, 7, 8] };
        shots.Add("party", shot);
        duels.Add("party", duel);
        BattleActionPresentation presentation = new("party", "enemy", BattleActionPhase.Recovery, 0, BattleAbility.LimitBreak, 0);

        Assert.True((bool)typeof(BattleSession).GetMethod("CompleteNativeShotAction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [presentation with { NativeLimitCallback = NativeLimitCallback.ShotEffect }])!);
        Assert.True((bool)typeof(BattleSession).GetMethod("CompleteNativeDuelAction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [presentation with { NativeLimitCallback = NativeLimitCallback.DuelEffect }])!);

        Assert.Equal(shot, shots["party"]);
        Assert.Equal(duel, duels["party"]);
    }
    private static BattleSession Fixture()
    {
        return new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
    }

    private static NativeBattleGraphicsState Graphics()
    {
        return new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel((byte)index, 0)).ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0);
    }

    private static T Field<T>(BattleSession session, string name)
    {
        return (T)typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    }

    private static NativeBattleGraphicsState Invoke(BattleSession session, NativeBattleGraphicsState graphics, uint callback)
    {
        return (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("InvokeNativeDescriptorCallback", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, callback, 0u])!;
    }
}
