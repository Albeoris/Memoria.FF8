using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7AutoPlayWorldSurvivalTests
{
    [Fact]
    public void OriginalExhaustedWorldPartyHealsThenRetreatsWithoutRevival()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_WORLD_SURVIVAL_SAVE");
        Assert.SkipUnless(ff7 is not null && ff8 is not null && path is not null, "Select original resources and an external exhausted-party world continuation.");
        byte[] bytes = File.ReadAllBytes(path!);
        using MemoryStream input = new(bytes, writable: false);
        CombinedSessionSave source = CombinedSessionCodec.Read(input);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(ff7!), source.Ff7!);
        for (int step = 0; step < 4 && session.Battle is null; step++)
        {
            session.AdvanceTick();
        }
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(session.Battle);
        using CrossoverMixedBattle mixed = new(ff8!, source with { Ff7 = session.Capture() }, battle);
        int healingCommands = 0;
        bool held = false;
        for (int tick = 0; tick < 5000 && battle.Outcome == Ff7BattleOutcome.Fighting; tick++)
        {
            if (!held && battle.ReadyPlayer is int actor)
            {
                var choice = Ff7Scene.PlanAutoPlayBattleCommand(battle.AutoPlayCommands(actor), battle.Actors, (command, target) => battle.GuestAbilityTargetUnsupportedReason(actor, command.Command, command.Argument, target) is null, (command, target) => battle.DescribeRecovery(actor, command, target));
                held = Ff7Scene.ShouldAutoPlayBattleRetreat(battle, choice);
                if (!held && choice is { } action)
                {
                    Assert.Equal(Ff7BattleRecoveryKind.HitPoints, battle.DescribeRecovery(actor, action.Command, action.TargetSlot));
                    Assert.True(battle.Submit(actor, action.Command.Command, action.Command.Argument, Ff7Scene.AutoPlayBattleTargetMask(action.Command, action.TargetSlot)));
                    healingCommands++;
                }
            }
            battle.Advance(escapeHeld: held);
        }
        Assert.Equal(1, healingCommands);
        Assert.True(held);
        Assert.Equal(Ff7BattleOutcome.Escaped, battle.Outcome);
        Assert.Equal(source.Ff7!.Progress.CompletedBattles.Length, session.Progress.CompletedBattles.Length);
        Assert.Equal(bytes, File.ReadAllBytes(path!));
    }
}
