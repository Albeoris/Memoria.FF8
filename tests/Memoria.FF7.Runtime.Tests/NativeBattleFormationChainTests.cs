using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleFormationChainTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original battle formations: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        initial.SetFullCombatEnabled(true);
        foreach ((ushort first, int second) in new[] { ((ushort)460, 461), ((ushort)462, 463) })
        {
            using Ff7Session host = Ff7Session.Restore(installation, initial.Capture());
            const long requestId = 1L << 40;
            host.Host.StartEncounter(new FieldHostRequest(requestId, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand(first)), 0);
            Ff7BattleSnapshot entry = host.Battle!.Capture();
            Check(host.Battle.Formation.NextBattleId == second && entry.Queue.Slots.Any(slot => slot?.Command.AttackId == 0x194), "Hundred Gunner must load the native successor and queue Glass Shatter during initialization.");
            Ff7BattleSnapshot prior = Victory(entry, 0);
            Ff7BattleActorState party = prior.Actors.First(actor => actor.Actor.Slot == 0);
            party = party with { Gauge = 40000, LimitGauge = 66, Actor = party.Actor with { HitPoints = 33, MagicPoints = 4, Statuses = 8 } };
            prior.Actors[0] = party;
            prior = prior with { Inventory = [new(0, 2)] };
            Ff7SessionSave before = host.Capture() with { Battle = prior };
            using Ff7Session live = Ff7Session.Restore(installation, before);
            using Ff7Session cold = Ff7Session.Restore(installation, Cold(before));
            Check(!live.Battle!.ResultReady && !live.ConfirmBattleVictory(), "Hundred Gunner must not publish a result before the successor loads.");
            live.AdvanceTick();
            cold.AdvanceTick();
            Ff7BattleSession next = live.Battle!;
            Ff7BattleSnapshot successor = next.Capture();
            Check(next.Formation.FormationId == second && next.RequestId == requestId && successor.InitialFormationId == first && live.Host.HasUnfinishedBattle && live.Progress.CompletedBattles.Length == 0, "Heli Gunner must retain the original field request without settling the first phase.");
            Check(successor.Actors[0] == party with { AiMemory = successor.Actors[0].AiMemory, AllUses = successor.Actors[0].AllUses } && successor.Inventory.SequenceEqual(prior.Inventory), "The successor changed the party's retained vitals, status, gauges or inventory.");
            Check(successor.Camera?.Scripts.InitialCamera == 0x54 && successor.Actors.Any(actor => actor.Identity == 67 && actor.Gauge is >= 0x8000 and <= 0xBF00) && successor.Queue.Slots.Any(slot => slot?.Command.AttackId is 0x011F or 0x019D), "The successor did not initialize its native enemy gauge, AI command or camera.");
            int randomDraws = unchecked((byte)(successor.Random.Indices[prior.Random.Stream] - prior.Random.Indices[prior.Random.Stream]));
            Check(successor.Random.CrtSeed == prior.Random.CrtSeed && randomDraws == 7, $"The successor did not continue the native random stream through AI initialization and six physical enemy gauge draws. Formation={second}, delta={randomDraws}.");
            Check(Serialize(live.Capture()) == Serialize(cold.Capture()), "The successor boundary changed after cold restore.");
            using Ff7Session reopened = Ff7Session.Restore(installation, Cold(live.Capture()));
            for (int callback = 0; callback < 117; callback++)
            {
                live.AdvanceTick();
                reopened.AdvanceTick();
                Check(Serialize(live.Capture()) == Serialize(reopened.Capture()), "The successor camera and live encounter did not resume identically after cold restore.");
            }
            Check(!live.Battle!.IntroActive && live.Battle.Capture().Intro is { Callbacks: 30, FormationSuccessor: true }, "The successor's original intro must release after thirty presentation callbacks.");

            using Ff7Session ending = Ff7Session.Restore(installation, Cold(live.Capture() with { Battle = Victory(live.Battle!.Capture(), 49) }));
            Ff7BattleReward reward = ending.PreviewBattleVictory().Reward;
            Check(reward is { Experience: 250, Gil: 200, AbilityPoints: 25 } && reward.Items.SequenceEqual(new[] { new Ff7RewardItem(259, 1) }), "The final settlement must award Heli Gunner's original values and Mythril Armlet.");
            Check(ending.ConfirmBattleVictory() && !ending.ConfirmBattleVictory() && !ending.Host.HasUnfinishedBattle && ending.Progress.CompletedBattles.Single() is { RequestId: requestId } receipt && receipt.Formation == second && ending.Progress.Gil == initial.Progress.Gil + 200, "The final formation must settle its reward and original field request exactly once.");
        }
    }

    private static Ff7BattleSnapshot Victory(Ff7BattleSnapshot snapshot, int terminalFrames)
    {
        Ff7BattleActorState[] actors = snapshot.Actors.Select(actor => actor.Actor.Slot < 4 ? actor : actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1 } }).ToArray();
        return snapshot with { Actors = actors, Outcome = Ff7BattleOutcome.Victory, TerminalFrames = terminalFrames };
    }

    private static Ff7SessionSave Cold(Ff7SessionSave save) => JsonSerializer.Deserialize<Ff7SessionSave>(Serialize(save))!;

    private static string Serialize(Ff7SessionSave save) => JsonSerializer.Serialize(save);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
