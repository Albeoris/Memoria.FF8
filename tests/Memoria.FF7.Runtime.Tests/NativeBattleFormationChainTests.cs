using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
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
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        foreach ((ushort first, int second) in new[] { ((ushort)460, 461), ((ushort)462, 463) })
        {
            using Ff7Session host = Ff7Session.Restore(installation, initial.Capture());
            const long requestId = 1L << 40;
            host.Host.StartEncounter(new FieldHostRequest(requestId, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand(first)), 0);
            Ff7BattleSnapshot entry = host.Battle!.Capture();
            Check(host.Battle.Formation.NextBattleId == second && entry.Queue.Slots.Any(slot => slot?.Command.AttackId == 0x194), "Hundred Gunner must load the native successor and queue Glass Shatter during initialization.");
            Ff7BattleSnapshot prior = Victory(entry, 1);
            Ff7BattleActorState party = prior.Actors.First(actor => actor.Actor.Slot == 0);
            party = party with { Gauge = 40000, LimitGauge = 66, Actor = party.Actor with { HitPoints = 33, MagicPoints = 4, Statuses = 8 } };
            prior.Actors[0] = party;
            Ff7BattleInventorySlot[] workingInventory = Ff7NativeBattleInventory.Create([new(0, 2)], kernel);
            prior = prior with { Inventory = Ff7NativeBattleInventory.Project(workingInventory), Economy = prior.Economy! with { Inventory = workingInventory, TerminalPasses = 16 } };
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

            Ff7BattleSnapshot terminal = Victory(live.Battle!.Capture(), 49);
            terminal = terminal with { Economy = terminal.Economy! with { TerminalPasses = 16 } };
            using Ff7Session ending = Ff7Session.Restore(installation, Cold(live.Capture() with { Battle = terminal }));
            Ff7BattleReward reward = ending.PreviewBattleVictory().Reward;
            Check(reward is { Experience: 250, Gil: 200, AbilityPoints: 25 } && reward.Items.SequenceEqual(new[] { new Ff7RewardItem(259, 1) }), "The final settlement must award Heli Gunner's original values and Mythril Armlet.");
            Check(ending.ConfirmBattleVictory() && !ending.ConfirmBattleVictory() && !ending.Host.HasUnfinishedBattle && ending.Progress.CompletedBattles.Single() is { RequestId: requestId } receipt && receipt.Formation == second && ending.Progress.Gil == initial.Progress.Gil + 200, "The final formation must settle its reward and original field request exactly once.");
        }
    }

    public static void RunGuestHojo()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original Hojo formations: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session host = Ff7Session.NewGame(installation);
        host.SetFullCombatEnabled(true);
        const long requestId = 1L << 40;
        host.Host.StartEncounter(new FieldHostRequest(requestId, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand(856)), 0);
        Ff7BattleSession entry = host.Battle!;
        Ff7BattleGuestVitalsBinding binding = new(Guid.Parse("4149fe66-fdda-44f2-837a-6eefe58614f1"), new string('A', 64), 0, 0, 0, 0, 623, 900, 41, 8, 55, 36);
        entry.BindGuestVitals(binding);
        entry.BindGuestSpeed(57);
        Ff7BattleGuestProjection projection = new(850, 41, 8, 56, 35);
        entry.ReprojectGuestVitals(projection);
        Ff7BattleSnapshot phase = entry.Capture();
        phase = phase with { Tick = 101, Queue = Ff7BattleCommandQueue.StageReady(phase.Queue, 0, 1, targetMask: 0x40, guestAttack: true) };
        phase.Actors[0] = phase.Actors[0] with { Gauge = ushort.MaxValue, Actor = phase.Actors[0].Actor with { HitPoints = 517 } };
        Ff7SessionSave save = host.Capture();

        foreach (int formation in new[] { 860, 864 })
        {
            using Ff7Session current = Ff7Session.Restore(installation, Cold(save with { Battle = Victory(phase, 1) }));
            Check(!current.ConfirmGuestBattleVictory(binding), "An intermediate phase must retain the field request.");
            for (int tick = 0; tick < 240 && current.Battle!.Formation.FormationId == phase.FormationId; tick++)
            {
                current.AdvanceTick();
            }
            Ff7BattleSession next = current.Battle!;
            Ff7BattleSnapshot successor = next.Capture();
            Check(successor.FormationId == formation && successor.InitialFormationId == 856 && successor.RequestId == requestId && successor.Tick >= phase.Tick && current.Progress.CompletedBattles.Length == 0, $"Hojo phase {phase.FormationId} reached {successor.FormationId} at {successor.Tick} with root {successor.InitialFormationId} and {current.Progress.CompletedBattles.Length} settlements.");
            Check(next.GuestVitals == binding && next.GuestProjection == projection && successor.GetGuestSlotStates().Single().BaseDexterity == 57 && successor.Actors[0].Actor.HitPoints == 517 && successor.Inventory.SequenceEqual(phase.Inventory), "Hojo's next phase lost the guest's live vitals, speed or inventory.");
            Check(successor.Actors[0].AwaitingTurn && successor.Queue.Ready.All(command => command?.GuestAttack != true) && successor.Queue.Slots.All(slot => slot?.Command.GuestAttack != true), "A queued guest action kept an old formation target or lost its turn.");
            save = current.Capture();
            phase = successor;
        }

        using Ff7Session ending = Ff7Session.Restore(installation, Cold(save with { Battle = Victory(phase, 49) }));
        for (int tick = 0; tick < 240 && !ending.Battle!.ResultReady; tick++)
        {
            ending.AdvanceTick();
        }
        Check(ending.ConfirmGuestBattleVictory(binding) && !ending.ConfirmGuestBattleVictory(binding) && ending.Progress.CompletedBattles.Single() is { RequestId: requestId, Formation: 864 }, "The chained encounter must settle the original request once, after the final formation.");
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
