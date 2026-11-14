using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleShinraTests
{
    public static void RunSample() => Run(456, [0, 1, 4]);

    public static void RunElevator()
    {
        Run(460, [1, 3, 4]);
        Run(462, [1, 3, 4]);
    }

    public static void RunRufus() => Run(464, [0, 255, 255]);

    private static void Run(ushort formation, int[] party)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original Shinra boss resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        Ff7Progress progress = initial.Progress with { Party = party, FullCombatEnabled = true, Inventory = [new(1, 20), new(7, 20), new(20, 30)] };
        using Ff7Session host = Ff7Session.Restore(installation, initial.Capture() with { Progress = progress });
        host.Host.StartEncounter(new FieldHostRequest(1L << 40, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand(formation)), 0);
        Ff7SessionSave entry = host.Capture();
        Ff7BattleSnapshot battle = entry.Battle!;
        foreach (int slot in Enumerable.Range(0, battle.Actors.Length).Where(index => battle.Actors[index].Actor.Slot < 3))
        {
            Ff7BattleActorState actor = battle.Actors[slot];
            battle.Actors[slot] = actor with { Actor = actor.Actor with { HitPoints = 1600, MaxHitPoints = 1600 } };
        }
        using Ff7Session combat = Ff7Session.Restore(installation, entry with { Battle = battle });
        HashSet<(int Formation, ushort Attack)> attacks = [];
        HashSet<int> phases = [];
        bool verifiedReportHelicopter = false;
        bool verifiedReportGain = false;
        for (int callback = 0; callback < 40000; callback++)
        {
            Ff7BattleSession previous = combat.Battle!;
            try
            {
                combat.AdvanceTick(advanceClock: true, turnBasedBattle: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Shinra formation {previous.Formation.FormationId} callback {callback}, action {previous.Action?.Command}, animation {previous.Action?.Animation}, attacks {string.Join(',', attacks)}.", ex);
            }
            Ff7BattleSession active = combat.Battle!;
            phases.Add(active.Formation.FormationId);
            if (formation == 464 && active.Action?.Command.AttackId == 0x0164)
            {
                Ff7BattleSnapshot presentation = active.Capture();
                Ff7BattleActorModelState helicopter = presentation.ActorModels.Single(model => model.Slot == 6);
                if (!verifiedReportHelicopter && helicopter is { LinkedAction: true, Action: 4, Modifiers.Visible: true, Pose.ClipIndex: 2 })
                {
                    CheckCold(combat, "Rufus linked helicopter action");
                    verifiedReportHelicopter = true;
                }
                if (!verifiedReportGain && presentation.SoundRequests.Any(sound => sound is { Channel: 1, ChannelGain: 0x50 }))
                {
                    CheckCold(combat, "Rufus Report sound gain");
                    verifiedReportGain = true;
                }
            }
            if (active.Action?.Command.AttackId is ushort attack && attacks.Add((active.Formation.FormationId, attack)))
            {
                CheckCold(combat, $"Shinra formation {active.Formation.FormationId} attack {attack:X4}");
            }
            if (active.ResultReady)
            {
                Check(active.Outcome == Ff7BattleOutcome.Victory && combat.ConfirmBattleVictory() && !combat.ConfirmBattleVictory(), $"Original Shinra formation {formation} did not settle once. Final phase={active.Formation.FormationId}, outcome={active.Outcome}.");
                Check(formation is not (460 or 462) || phases.SetEquals(new[] { (int)formation, formation + 1 }), "The elevator fixture must run both authored formations.");
                Check(formation != 464 || verifiedReportHelicopter && verifiedReportGain, $"Rufus's final action must advance the linked helicopter and issue its original gain. Helicopter={verifiedReportHelicopter}, gain={verifiedReportGain}.");
                Check(attacks.Count > 0, $"Original Shinra formation {formation} did not execute a native enemy action.");
                return;
            }
            if (active.ReadyPlayer is not int owner)
            {
                continue;
            }
            Ff7BattleActorState actorState = active.Actors.Single(actor => actor.Actor.Slot == owner);
            Ff7BattleCommandOption[] commands = active.Commands(owner).ToArray();
            Ff7BattleCommandOption? command = actorState.Actor.HitPoints < 350 ? commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Item && option.Argument == 1) : null;
            int target = owner;
            if (command is null)
            {
                command = commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Limit && (option.ValidTargets & 0x3F0) != 0)
                    ?? commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Magic && option.Argument is 0x21 or 0x1E or 0x1B && (option.ValidTargets & 0x3F0) != 0)
                    ?? commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Item && option.Argument == 20 && (option.ValidTargets & 0x3F0) != 0)
                    ?? commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Attack && (option.ValidTargets & 0x3F0) != 0)
                    ?? commands.Single(option => option.Command == Ff7BattleCommand.Defend);
                ushort enemies = (ushort)(command.ValidTargets & 0x3F0);
                if (enemies != 0)
                {
                    target = Enumerable.Range(4, 6).Where(slot => (enemies & 1 << slot) != 0).Max();
                }
            }
            Check(active.Submit(owner, command.Command, command.Argument, (ushort)(1 << target)), $"Original Shinra fixture could not submit {command.Command}/{command.Argument:X2} from slot {owner} to slot {target}.");
        }
        Ff7BattleSnapshot last = combat.Battle!.Capture();
        string vitals = string.Join(',', last.Actors.Select(actor => $"{actor.Actor.Slot}:{actor.Actor.HitPoints}/{actor.Actor.MagicPoints}/{actor.Actor.Statuses:X8}/{actor.Gauge}"));
        throw new InvalidOperationException($"Shinra formation {formation} exhausted its turn budget. Phases={string.Join(',', phases)}, attacks={string.Join(',', attacks)}, vitals={vitals}, clock paused={last.ActionClockPaused}, action={last.Action?.Command}.");

        void CheckCold(Ff7Session source, string owner)
        {
            string snapshot = JsonSerializer.Serialize(source.Capture());
            using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(snapshot)!);
            source.AdvanceTick(advanceClock: true, turnBasedBattle: true);
            cold.AdvanceTick(advanceClock: true, turnBasedBattle: true);
            Check(JsonSerializer.Serialize(source.Capture()) == JsonSerializer.Serialize(cold.Capture()), $"Original {owner} diverged after a cold continuation.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
