using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleMindblowTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original Mindblow resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        CheckCallbackTrajectory();
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7CharacterState[] characters = original.Progress.Characters.ToArray();
        byte[] native = (byte[])characters[1].NativeRecord.Clone();
        native[14] = 1;
        native[15] = byte.MaxValue;
        native[34] = 3;
        characters[1] = characters[1] with { NativeRecord = native };
        Ff7Progress progress = original.Progress with { Party = [1, 255, 255], Characters = characters, FullCombatEnabled = true };
        using Ff7Session host = Ff7Session.Restore(installation, original.Capture() with { Progress = progress });
        host.Host.StartEncounter(new FieldHostRequest(1L << 40, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand(300)), 0);
        Ff7SessionSave entry = host.Capture();
        Ff7BattleSnapshot snapshot = entry.Battle!;
        int target = Array.FindIndex(snapshot.Actors, actor => actor.Actor.Slot == 4);
        Ff7BattleActorState enemy = snapshot.Actors[target];
        snapshot.Actors[target] = enemy with { Actor = enemy.Actor with { MagicPoints = 51, MaxMagicPoints = 51 } };
        using Ff7Session battleHost = Ff7Session.Restore(installation, entry with { Battle = snapshot });
        Ff7BattleSession battle = battleHost.Battle!;
        Ff7Session? cold = null;
        bool submitted = false;
        bool foundEffect = false;
        bool hit = false;
        bool sawMesh = false;
        bool sawParticle = false;
        int launchAudio = 0;
        try
        {
            for (int frame = 0; frame < 3800 && !battle.ResultReady; frame++)
            {
                battle.Advance(turnBased: true);
                Ff7BattleSoundReceipt[] audio = battle.DrainSoundRequests();
                if (cold is not null)
                {
                    cold.Battle!.Advance(turnBased: true);
                    Check(audio.SequenceEqual(cold.Battle.DrainSoundRequests()) && JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Battle.Capture()), "Mindblow diverged after its cold callback restore.");
                }
                if (battle.Action is { Command: { Argument: 0x89 } } action)
                {
                    foundEffect = true;
                    launchAudio += audio.Count(cue => cue.Sound == 40);
                    Check(action.Animation == 0x3D && action.Command.TargetMask == 0x10, "Mindblow requires its native second level-one action and single target.");
                    sawMesh |= battle.PrimaryEffects?.Draws.Any(draw => draw is Ff7BattlePrimaryMindblowMeshDraw) == true;
                    sawParticle |= battle.PrimaryEffects?.Draws.Any(draw => draw is Ff7BattlePrimaryMindblowParticleDraw) == true;
                    if (cold is null && sawMesh && sawParticle)
                    {
                        cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(battleHost.Capture()))!);
                    }
                    if (battle.DisplayedHit is { AttackId: 0x89, Target: 4, Damage: { Missed: false, Damage: 51 } })
                    {
                        hit = true;
                        Ff7BattleActorState contacted = battle.Actors[target];
                        Check(contacted.Actor.MagicPoints == 0 && contacted.Actor.HitPoints == enemy.Actor.HitPoints, "Mindblow did not consume current enemy MP independently of HP.");
                    }
                }
                else if (foundEffect && cold is not null)
                {
                    cold.Dispose();
                    cold = null;
                }
                if (battle.ReadyPlayer is int ready)
                {
                    if (!submitted)
                    {
                        Check(battle.Submit(ready, Ff7BattleCommand.Limit, 0x89, 0x10), "Barret could not submit original learned Mindblow.");
                        submitted = true;
                    }
                    else if (hit && battle.Actors.FirstOrDefault(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints > 0) is { } living)
                    {
                        Check(battle.Submit(ready, Ff7BattleCommand.Attack, 0, (ushort)(1 << living.Actor.Slot)), "Barret could not finish the original formation.");
                    }
                }
            }
        }
        finally
        {
            cold?.Dispose();
        }
        Check(foundEffect && sawMesh && sawParticle && hit && launchAudio == 1 && battle.Outcome == Ff7BattleOutcome.Victory && battle.ResultReady, $"Mindblow did not finish its native action and victory. Effect={foundEffect}, mesh={sawMesh}, particle={sawParticle}, hit={hit}, audio={launchAudio}, outcome={battle.Outcome}, last={battle.LastHit}.");
        Ff7VictoryProjection preview = battleHost.PreviewBattleVictory();
        Check(preview.Progress.Characters[1].NativeRecord[15] == battle.Actors[0].LimitGauge && battleHost.ConfirmBattleVictory() && !battleHost.ConfirmBattleVictory(), "Mindblow victory must publish the spent gauge and settle once.");
        battleHost.Host.ResumeEncounter();
    }

    private static void CheckCallbackTrajectory()
    {
        int samples = 0;
        Ff7BattlePrimaryContext context = new((_, _, offset) => offset, _ => default, _ => 200, () => ++samples, JointPoint: (_, _) => new(0, 0, 1200), WeaponPoint: (_, offset) => offset);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleMindblow(new(), 0, 0x10);
        for (int age = 0; age <= 44; age++)
        {
            int before = samples;
            primary = Ff7BattlePrimaryEffects.Advance(primary, context);
            Check(samples - before == (age < 14 ? 8 : age is >= 22 and < 25 ? 100 : 0), "Mindblow changed the native charge or impact random draw count.");
            Check(primary.ImpactTargets == (age == 22 ? 0x10 : 0), "Mindblow changed the original controller contact callback.");
            if (age == 0)
            {
                Check(primary.Draws.OfType<Ff7BattlePrimaryMindblowMeshDraw>().Count() == 2 && primary.PositionedSounds.Single().Sound == 40, "Mindblow did not start its native dual mesh and audio on the first callback.");
            }
            if (age is 22 or 23)
            {
                Check(primary.Draws.OfType<Ff7BattlePrimaryMindblowPulseDraw>().Single().Brightness == (age == 22 ? 255 : 127), "Mindblow changed the original two-callback screen pulse.");
            }
            if (age == 24)
            {
                Check(primary.Draws.OfType<Ff7BattlePrimaryMindblowParticleDraw>().Count() == 60, "Mindblow did not retain three original waves of 20 impact particles.");
            }
        }
        Check(!primary.HasCallbacks && samples == 412, "Mindblow retained an expired callback or consumed extra native random samples.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
