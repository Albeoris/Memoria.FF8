using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class InstalledOpeningCombatMatrixTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP FFVII opening combat matrix: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> scenes = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic);
        IReadOnlyList<Ff7BattleAttack> limits = Ff7PcExecutableData.LoadBattleLimits(installation);
        IReadOnlyList<byte> reel = Ff7PcExecutableData.LoadBeatRushReel(installation);
        int[] opening = [.. Enumerable.Range(300, 25), 328, 329, 330, .. Enumerable.Range(332, 28), 364, 367, .. Enumerable.Range(392, 4)];
        List<string> errors = [];
        foreach (int id in opening)
        {
            SceneFormation formation = scenes.Single(scene => scene.FormationId == id);
            int[] party = id switch
            {
                < 305 or 328 or 329 or 330 => [0, 255, 255],
                < 325 or >= 392 => [0, 1, 255],
                364 => [1, 0, 2],
                _ => [0, 1, 2]
            };
            Ff7Progress progress = original.Progress with { Party = party };
            try
            {
                Fight(formation, progress, kernel, executable.NativeMateriaStatModifiers, animations, limits);
            }
            catch (Exception error) when (error is InvalidOperationException or InvalidDataException or NotSupportedException or ArgumentException)
            {
                string location = error.StackTrace?.Split('\n').FirstOrDefault(line => line.Contains("InstalledOpeningCombatMatrixTests.cs", StringComparison.Ordinal))?.Trim() ?? "";
                errors.Add($"{id}: {error.Message} {location}");
            }
        }
        if (errors.Count != 0)
        {
            throw new InvalidOperationException($"{opening.Length - errors.Count}/{opening.Length} opening formations ended in victory.\n{string.Join("\n", errors)}");
        }
        Console.WriteLine($"Original FFVII opening combat matrix: {opening.Length} completed formations with cold continuation.");
        CheckPlayerCommands(scenes.Single(scene => scene.FormationId == 300), original.Progress, kernel, executable.NativeMateriaStatModifiers, animations, limits, reel);
        CheckEnemyActions(scenes.Where(scene => opening.Contains(scene.FormationId)), original.Progress, kernel, executable.NativeMateriaStatModifiers, animations);
    }

    private static void CheckPlayerCommands(SceneFormation formation, Ff7Progress start, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> materiaModifiers, Ff7BattleAnimationResources animations, IReadOnlyList<Ff7BattleAttack> limits, IReadOnlyList<byte> reel)
    {
        Check(0, Ff7BattleCommand.Attack, 0);
        Check(0, Ff7BattleCommand.Magic, 0x1E);
        Check(0, Ff7BattleCommand.Magic, 0x21);
        Check(0, Ff7BattleCommand.Magic, 0, 0x35, group: true);
        Check(0, Ff7BattleCommand.Magic, 0x1B, 0x31);
        Check(0, Ff7BattleCommand.Item, 0);
        Check(0, Ff7BattleCommand.Item, 3);
        Check(0, Ff7BattleCommand.Item, 7);
        Check(0, Ff7BattleCommand.Item, 8);
        Check(0, Ff7BattleCommand.Item, 20);
        Check(0, Ff7BattleCommand.Defend, 0);
        Check(0, Ff7BattleCommand.Change, 0);
        Check(0, Ff7BattleCommand.Limit, 0x80);
        Check(1, Ff7BattleCommand.Limit, 0x87);
        Check(2, Ff7BattleCommand.Limit, 0x95);
        Console.WriteLine("Opening player Attack, spells, linked All, items, Change, Defend and three Limit commands reached completion.");

        void Check(int characterId, Ff7BattleCommand command, ushort argument, byte? optionalMateria = null, bool group = false)
        {
            Ff7CharacterState[] characters = (Ff7CharacterState[])start.Characters.Clone();
            int characterIndex = Array.FindIndex(characters, value => value.Id == characterId);
            Ff7CharacterState character = characters[characterIndex];
            byte[] record = (byte[])character.NativeRecord.Clone();
            if (optionalMateria is byte materia)
            {
                Array.Clear(record, 64, 8);
                record[64] = materia;
                record[68] = 0x17;
            }
            if (command == Ff7BattleCommand.Limit)
            {
                record[15] = byte.MaxValue;
            }
            characters[characterIndex] = character with { NativeRecord = record };
            bool revive = command == Ff7BattleCommand.Item && argument == 7;
            Ff7Progress progress = start with { Party = [characterId, revive ? 1 : 255, 255], Characters = characters, Inventory = [new(0, 3), new(3, 1), new(7, 1), new(8, 1), new(20, 1)] };
            Ff7BattleSession initial = new(formation, progress, kernel, materiaModifiers, 1, animations: animations, limits: limits, beatRushSymbols: reel);
            Ff7BattleSnapshot snapshot = initial.Capture();
            Ff7BattleActorState player = snapshot.Actors[0];
            uint statuses = player.Actor.Statuses | (command == Ff7BattleCommand.Item && argument == 8 ? 8u : 0u);
            snapshot.Actors[0] = player with { AwaitingTurn = true, Gauge = ushort.MaxValue, Actor = player.Actor with { HitPoints = player.Actor.HitPoints / 2, MagicPoints = (ushort)(player.Actor.MagicPoints / 2), Statuses = statuses } };
            if (revive)
            {
                Ff7BattleActorState down = snapshot.Actors[1];
                snapshot.Actors[1] = down with { Actor = down.Actor with { HitPoints = 0, Statuses = 1 } };
            }
            Ff7BattleSession battle = new(formation, progress, kernel, materiaModifiers, 1, snapshot: snapshot, animations: animations, limits: limits, beatRushSymbols: reel);
            Ff7BattleCommandOption option = battle.Commands(0).Single(candidate => candidate.Command == command && candidate.Argument == argument);
            ushort targets = revive ? (ushort)2 : option.DefaultsToEnemies ? (ushort)(group ? 0x30 : 0x10) : (ushort)1;
            if (!battle.Submit(0, command, argument, targets, group))
            {
                throw new InvalidOperationException($"Opening player command {command}/{argument:X2} was rejected.");
            }
            for (int tick = 0; tick < 1500; tick++)
            {
                battle.Advance(confirmLimit: tick == 55);
                Ff7BattleActorState current = battle.Actors[0];
                if (battle.Action is null && (battle.LastHit?.Source == 0 || current.Defending || (current.Actor.Conditions & 0x40) != (player.Actor.Conditions & 0x40)))
                {
                    if (command == Ff7BattleCommand.Limit && current.LimitGauge != 0 || command == Ff7BattleCommand.Magic && current.Actor.MagicPoints >= snapshot.Actors[0].Actor.MagicPoints || group && current.AllUses?.GetValueOrDefault((byte)argument) != option.GroupUses - 1)
                    {
                        throw new InvalidOperationException($"Opening player command {command}/{argument:X2} retained a spent resource.");
                    }
                    if (revive && (battle.Actors[1].Actor.HitPoints == 0 || (battle.Actors[1].Actor.Statuses & 1) != 0))
                    {
                        throw new InvalidOperationException("Phoenix Down did not restore its knocked-out party recipient.");
                    }
                    return;
                }
            }
            throw new InvalidOperationException($"Opening player command {command}/{argument:X2} did not finish.");
        }
    }

    private static void CheckEnemyActions(IEnumerable<SceneFormation> formations, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> materiaModifiers, Ff7BattleAnimationResources animations)
    {
        HashSet<(short Enemy, ushort Attack, byte Animation)> checkedActions = [];
        foreach (SceneFormation formation in formations)
        {
            SceneBattleData data = SceneBattleData.Read(formation);
            foreach (SceneBattleEnemy enemy in data.Enemies)
            {
                foreach (SceneEnemyAttack selector in enemy.Attacks.Where(attack => attack.ActionAnimation != byte.MaxValue))
                {
                    SceneBattleAttack? record = data.Attacks.FirstOrDefault(candidate => candidate.Id == selector.AttackId);
                    if (record is null || !checkedActions.Add((enemy.Placement.EnemyId, selector.AttackId, selector.ActionAnimation)))
                    {
                        continue;
                    }
                    Ff7BattleSession initial = new(formation, progress, kernel, materiaModifiers, 1, animations: animations);
                    Ff7BattleSnapshot snapshot = initial.Capture();
                    int slot = enemy.FormationSlot + 4;
                    ushort targets = record.Data.TargetFlags == 0 ? (ushort)(1 << slot) : (ushort)1;
                    Ff7BattleQueuedCommand command = new(2, slot, 0x20, (ushort)record.Slot, targets, selector.AttackId);
                    snapshot = snapshot with { Queue = Ff7BattleCommandQueue.Enqueue(snapshot.Queue, command) };
                    Ff7BattleSession battle = new(formation, progress, kernel, materiaModifiers, 1, snapshot: snapshot, animations: animations);
                    bool active = false;
                    try
                    {
                        for (int tick = 0; tick < 3500; tick++)
                        {
                            battle.Advance(waitForCommand: true);
                            active |= battle.Action?.Command.AttackId == selector.AttackId;
                            if (active && battle.Action is null)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
                    {
                        throw new InvalidOperationException($"Enemy {enemy.Placement.EnemyId}, original action {selector.AttackId:X4}/{selector.ActionAnimation} in formation {formation.FormationId}.", exception);
                    }
                    if (!active || battle.Action is not null)
                    {
                        throw new InvalidOperationException($"Enemy {enemy.Placement.EnemyId} original action {selector.AttackId:X4}/{selector.ActionAnimation} did not complete.");
                    }
                }
            }
        }
        Console.WriteLine($"Original FFVII opening enemy action matrix: {checkedActions.Count} distinct prototype, attack and animation combinations.");
    }

    private static void Fight(SceneFormation formation, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> materiaModifiers, Ff7BattleAnimationResources animations, IReadOnlyList<Ff7BattleAttack> limits)
    {
        Ff7BattleSession battle = new(formation, progress, kernel, materiaModifiers, 1, animations: animations, limits: limits);
        bool restored = false;
        for (int tick = 0; tick < 28000 && battle.Outcome == Ff7BattleOutcome.Fighting; tick++)
        {
            if (battle.ReadyPlayer is int slot)
            {
                Ff7BattleActorState own = battle.Actors.Single(actor => actor.Actor.Slot == slot);
                IReadOnlyList<Ff7BattleCommandOption> commands = battle.Commands(slot);
                Ff7BattleCommandOption selected = commands.FirstOrDefault(command => command.ValidTargets != 0 && command.Command == Ff7BattleCommand.Item && command.Argument == 7)
                    ?? commands.FirstOrDefault(command => command.ValidTargets != 0 && command.Command == Ff7BattleCommand.Item && command.Argument == 0 && own.Actor.HitPoints * 3 < own.Actor.MaxHitPoints)
                    ?? commands.FirstOrDefault(command => command.ValidTargets != 0 && command.Command == Ff7BattleCommand.Limit)
                    ?? commands.FirstOrDefault(command => command.ValidTargets != 0 && command.Command == Ff7BattleCommand.Magic && command.Argument == 0x21)
                    ?? commands.FirstOrDefault(command => command.ValidTargets != 0 && command.Command == Ff7BattleCommand.Attack)
                    ?? commands.First(command => command.Command == Ff7BattleCommand.Defend);
                Ff7BattleActorState target = battle.Actors.Where(actor => (selected.ValidTargets & 1 << actor.Actor.Slot) != 0).OrderByDescending(actor => (actor.Actor.Slot >= 4) == selected.DefaultsToEnemies).ThenBy(actor => actor.Actor.HitPoints).First();
                if (!battle.Submit(slot, selected.Command, selected.Argument, (ushort)(1 << target.Actor.Slot)))
                {
                    throw new InvalidOperationException("An admitted command was rejected.");
                }
            }
            battle.Advance();
            if (!restored && battle.LastHit is not null)
            {
                string before = JsonSerializer.Serialize(battle.Capture());
                Ff7BattleSession cold = new(formation, progress, kernel, materiaModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(before), animations: animations, limits: limits);
                battle.Advance();
                cold.Advance();
                if (JsonSerializer.Serialize(battle.Capture()) != JsonSerializer.Serialize(cold.Capture()))
                {
                    throw new InvalidOperationException("The battle continuation diverged after cold restore.");
                }
                battle = cold;
                restored = true;
            }
        }
        if (battle.Outcome != Ff7BattleOutcome.Victory || !restored)
        {
            throw new InvalidOperationException($"Battle ended at tick {battle.Tick} with {battle.Outcome} and last hit {battle.LastHit}.");
        }
    }
}
