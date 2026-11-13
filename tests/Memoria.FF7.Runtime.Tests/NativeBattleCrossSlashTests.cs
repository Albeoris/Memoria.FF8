using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleCrossSlashTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original Cross-slash resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7CharacterState[] characters = (Ff7CharacterState[])original.Progress.Characters.Clone();
        byte[] cloud = (byte[])characters[0].NativeRecord.Clone();
        cloud[15] = byte.MaxValue;
        cloud[34] = 3;
        characters[0] = characters[0] with { NativeRecord = cloud };
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255], Characters = characters, FullCombatEnabled = true };
        using Ff7Session host = Ff7Session.Restore(installation, original.Capture() with { Progress = progress });
        host.Host.StartEncounter(new FieldHostRequest(1L << 40, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand(300)), 0);
        Ff7SessionSave entry = host.Capture();
        Ff7BattleSnapshot initial = entry.Battle!;
        for (int index = 0; index < initial.Actors.Length; index++)
        {
            Ff7BattleActorState actor = initial.Actors[index];
            if (actor.Actor.Slot == 4)
            {
                initial.Actors[index] = actor with { Actor = actor.Actor with { HitPoints = 300, MaxHitPoints = 300 } };
            }
        }
        using Ff7Session battleHost = Ff7Session.Restore(installation, entry with { Battle = initial });
        Ff7BattleSession battle = battleHost.Battle!;
        HashSet<int> sprites = [];
        List<ushort> sounds = [];
        Ff7Session? cold = null;
        bool submitted = false;
        bool contacted = false;
        bool finished = false;
        try
        {
            for (int step = 0; step < 6000 && !battle.ResultReady; step++)
            {
                battle.Advance(turnBased: true);
                Ff7BattleSoundReceipt[] emitted = battle.DrainSoundRequests();
                if (cold is not null)
                {
                    cold.Battle!.Advance(turnBased: true);
                    Check(emitted.SequenceEqual(cold.Battle.DrainSoundRequests()) && Snapshot(battle) == Snapshot(cold.Battle), "Cross-slash diverged after a cold callback restore.");
                }
                if (battle.Action is { Command: { ActorSlot: 0, Argument: 0x81 } } action)
                {
                    Check(action.Animation == 0x3D && action.Command.TargetMask == 0x10, "Cross-slash did not start Cloud's second learned action against the selected target.");
                    sounds.AddRange(emitted.Where(cue => cue.Sound is 18 or 27 or 33).Select(cue => cue.Sound));
                    foreach (Ff7BattlePrimaryCrossSlashDraw draw in battle.PrimaryEffects?.Draws.OfType<Ff7BattlePrimaryCrossSlashDraw>() ?? [])
                    {
                        sprites.Add(draw.Sprite);
                    }
                    Ff7BattleActorState target = battle.Actors.Single(actor => actor.Actor.Slot == 4);
                    if (!contacted && battle.LastHit is { AttackId: 0x81, Target: 4, Damage.Missed: false })
                    {
                        Check(target.Actor.HitPoints is > 0 and < 300 && (target.Actor.Statuses & 0x02000000) != 0, $"Cross-slash did not apply its original damage and Paralysis. HP={target.Actor.HitPoints}, statuses={target.Actor.Statuses:X8}, hit={battle.LastHit}.");
                        contacted = true;
                    }
                    if (cold is null && sprites.Count == 2)
                    {
                        cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(battleHost.Capture()))!);
                    }
                }
                else if (contacted && !finished)
                {
                    Check(cold is not null, "Cross-slash never reached its in-flight save boundary.");
                    cold!.Dispose();
                    cold = null;
                    finished = true;
                }
                if (battle.ReadyPlayer is int ready)
                {
                    if (!submitted)
                    {
                        Check(battle.Commands(ready).Any(option => option.Command == Ff7BattleCommand.Limit && option.Argument == 0x80), "Cloud's preceding learned Limit is missing.");
                        Check(battle.Submit(ready, Ff7BattleCommand.Limit, 0x81, 0x10), "Cloud could not submit learned Cross-slash through the command menu.");
                        submitted = true;
                    }
                    else if (finished && battle.Actors.FirstOrDefault(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints > 0) is { } target)
                    {
                        Check(battle.Submit(ready, Ff7BattleCommand.Attack, 0, (ushort)(1 << target.Actor.Slot)), "Cloud could not finish the original formation.");
                    }
                }
            }
        }
        finally
        {
            cold?.Dispose();
        }
        Check(finished && sprites.SetEquals([0, 1, 2]) && sounds.SequenceEqual(new ushort[] { 18, 18, 27, 33 }), $"Cross-slash did not retire all sprite families and ordered sample requests. Finished={finished}, sprites={string.Join(',', sprites)}, sounds={string.Join(',', sounds)}.");
        Check(battle.ResultReady && battle.Outcome == Ff7BattleOutcome.Victory, "Cross-slash fixture did not reach original formation victory.");
        Ff7VictoryProjection preview = battleHost.PreviewBattleVictory();
        Check(preview.Progress.Characters[0].NativeRecord[15] == battle.Actors[0].LimitGauge && preview.Progress.Characters[0].NativeRecord[15] < byte.MaxValue, "Cross-slash gauge consumption did not reach the native character record.");
        Check(battleHost.ConfirmBattleVictory() && !battleHost.ConfirmBattleVictory() && battleHost.Battle is null, "Cross-slash formation did not settle once.");
        battleHost.Host.ResumeEncounter();
    }

    private static string Snapshot(Ff7BattleSession battle) => JsonSerializer.Serialize(battle.Capture());

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
