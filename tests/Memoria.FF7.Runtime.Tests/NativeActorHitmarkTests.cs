using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeActorHitmarkTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed D7 actor hitmarks: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[84];
        IReadOnlyList<short> materia = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        string prefix = Ff7BattleAnimationResources.ModelPrefix(formation.Enemies[4].EnemyId, true);
        BattleActionBank bank = BattleActionBank.Read(models.ReadEntry(models.Find(prefix + "ab").Single()));
        BattleModelData model = BattleModelData.Read(models.ReadEntry(models.Find(prefix + "aa").Single()), models.ReadEntry(models.Find(prefix + "da").Single()));
        foreach ((int action, byte delay, byte style) in new (int, byte, byte)[] { (19, 0, 0x2C), (20, 2, 0x2B), (21, 4, 0x2A) })
        {
            BattleActionInstruction instruction = BattleActionInstructions.ReadUntilIdle(bank.Scripts[action], model.BodyClips.Count).Single(instruction => instruction.Opcode == 0xD7);
            Check(instruction.Operands.Span.SequenceEqual(new byte[] { delay, style }), "Original D7 animation operands changed their delay/style contract.");
        }
        Ff7BattleSession initial = new(formation, progress, kernel, materia, 1, animations: animations);
        Ff7BattleSnapshot source = initial.Capture();
        source = source with
        {
            Intro = null,
            Actors = source.Actors.Select(actor => actor with { AwaitingTurn = actor.Actor.Slot == 0, Gauge = actor.Actor.Slot == 0 ? ushort.MaxValue : (ushort)0 }).ToArray(),
            ActorModels = source.ActorModels.Select(actor => actor.Slot == 8 ? actor with { Action = 19, Pose = null, Reaction = true, Ready = false, PendingSince = -1, IdleStarted = null } : actor).ToArray()
        };
        Ff7BattleSession battle = Restore(source);
        Ff7BattleSession? cold = null;
        HashSet<int> owners = [];
        HashSet<int> pendingStyles = [];
        bool retired = false;
        for (int tick = 0; tick < 200 && !retired; tick++)
        {
            battle.Advance(waitForCommand: true);
            Ff7BattleSnapshot current = battle.Capture();
            if (cold is not null)
            {
                cold.Advance(waitForCommand: true);
                Check(JsonSerializer.Serialize(current) == JsonSerializer.Serialize(cold.Capture()), "Restoring the actor hitmark producer changed its next callback or frozen anchor.");
            }
            if (current.SecondaryEffects is { } secondary)
            {
                foreach (Ff7BattleDelayedActorHitmarkCallback pending in secondary.Slots.OfType<Ff7BattleDelayedActorHitmarkCallback>())
                {
                    pendingStyles.Add(pending.Definition.Id);
                }
                foreach (Ff7BattleHitmarkState hitmark in secondary.Hitmarks)
                {
                    owners.Add(hitmark.Target);
                    Check(hitmark.Target is >= 4 and <= 8 && hitmark.Anchor is not null && hitmark.CollisionRadius == animations.CollisionRadius(Ff7BattleAnimationResources.ModelPrefix(formation.Enemies[hitmark.Target - 4].EnemyId, true)), "D7 did not bind the executing actor's original frozen joint/radius.");
                    Check(!hitmark.Flipped, "D7's BYTE style must not inherit a numerical hitmark orientation bit.");
                }
                if (cold is null && secondary.Slots.OfType<Ff7BattleDelayedActorHitmarkCallback>().Any())
                {
                    cold = Restore(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(current))!);
                }
                retired = owners.Count == 5 && battle.NativeActorModelsReady && !secondary.HasCallbacks;
            }
        }
        Check(retired && cold is not null && owners.SetEquals(new[] { 4, 5, 6, 7, 8 }) && pendingStyles.Contains(0x2A) && pendingStyles.Contains(0x2B), "Original linked D7 hitmarks2A/2B/2C did not dispatch, render and retire for their own actors.");
        Check(JsonSerializer.Serialize(source.Actors.Select(actor => actor.Actor)) == JsonSerializer.Serialize(battle.Capture().Actors.Select(actor => actor.Actor)), "The model-event hitmark path modified native combatant vitals or conditions.");
        Console.WriteLine("PASS original D7 actor hitmarks2A/2B/2C, linked ownership, cold callback and retirement.");

        Ff7BattleSession Restore(Ff7BattleSnapshot snapshot) => new(formation, progress, kernel, materia, 1, snapshot: snapshot, animations: animations);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
