using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7AutoPlayRetreatTests
{
    [Fact]
    public void OriginalCriticalPartyUsesNativeEscapeAndRestoresItsHeldInput()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_RETREAT_SAVE");
        Assert.SkipUnless(ff7 is not null && ff8 is not null && path is not null, "Select original resources and an external critical-party continuation.");
        byte[] bytes = File.ReadAllBytes(path!);
        using MemoryStream stream = new(bytes, writable: false);
        CombinedSessionSave source = CombinedSessionCodec.Read(stream);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session session = Ff7Session.Restore(installation, source.Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the supplied encounter."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 256 && session.Battle is null && controller.Running; step++)
        {
            controller.Advance(1);
        }
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(session.Battle);
        using CrossoverMixedBattle mixed = new(ff8!, source with { Ff7 = session.Capture() }, battle);
        Ff7Session? cold = null;
        CrossoverMixedBattle? coldMixed = null;
        bool requested = false;
        try
        {
            for (int tick = 0; tick < 2000 && battle.Outcome == Ff7BattleOutcome.Fighting; tick++)
            {
                bool held = ChooseEscape(battle);
                requested |= held;
                battle.Advance(escapeHeld: held);
                if (cold is not null)
                {
                    cold.Battle!.Advance(escapeHeld: ChooseEscape(cold.Battle));
                }
                if (cold is null && battle.EscapeInProgress && battle.Tick >= 450)
                {
                    using MemoryStream snapshot = new();
                    CombinedSessionCodec.Write(snapshot, source with { Ff7 = session.Capture(), MixedBattlePayload = mixed.Capture() });
                    snapshot.Position = 0;
                    CombinedSessionSave saved = CombinedSessionCodec.Read(snapshot);
                    cold = Ff7Session.Restore(installation, saved.Ff7!);
                    coldMixed = new(ff8!, saved, cold.Battle!);
                    Assert.True(cold.Battle!.EscapeInProgress);
                }
            }
            Assert.True(requested);
            Assert.Equal(Ff7BattleOutcome.Escaped, battle.Outcome);
            Assert.NotNull(cold);
            Assert.Equal(Ff7BattleOutcome.Escaped, cold!.Battle!.Outcome);
            Assert.Equal(battle.Tick, cold.Battle.Tick);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Battle.Capture()));
            Assert.Equal(source.Ff7!.Progress.CompletedBattles.Length, session.Progress.CompletedBattles.Length);
            Assert.Equal(bytes, File.ReadAllBytes(path!));
        }
        finally
        {
            coldMixed?.Dispose();
            cold?.Dispose();
        }
    }

    private static bool ChooseEscape(Ff7BattleSession battle)
    {
        if (battle.EscapeInProgress && battle.CanAttemptEscape)
        {
            return true;
        }
        if (battle.ReadyPlayer is not int owner)
        {
            return false;
        }
        var choice = Ff7Scene.PlanAutoPlayBattleCommand(battle.AutoPlayCommands(owner), battle.Actors, (command, target) => battle.GuestAbilityTargetUnsupportedReason(owner, command.Command, command.Argument, target) is null, (command, target) => battle.DescribeRecovery(owner, command, target));
        bool retreat = Ff7Scene.ShouldAutoPlayBattleRetreat(battle, choice);
        Assert.True(retreat, "The supplied original critical party unexpectedly selected ordinary combat before retreat.");
        return retreat;
    }
}
