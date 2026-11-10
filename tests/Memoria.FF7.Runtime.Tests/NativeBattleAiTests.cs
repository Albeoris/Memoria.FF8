using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeBattleAiTests
{
    private sealed record TurnBoundary(ushort[] Gauges, Ff7BattleRngState Random, Dictionary<int, byte[]> Locals, Ff7BattleAiCommand[] Pending);
    private sealed record BossBoundary(Ff7BattleAiActor[] Actors, byte[] Locals, Ff7BattleRngState Random, Ff7BattleAiCommand[] Commands);

    public static void RunContracts()
    {
        byte[] kernel = new byte[3988];
        kernel[0xE1C] = 1;
        Ff7BattleRng random = new(kernel, new(new byte[8]));
        SceneBattleData formation = new(1, [], [], [new(7, 272, ReadOnlyMemory<byte>.Empty, Ff7BattleAttack.Read(new byte[28]))], null);
        Ff7BattleAiContext context = new(4, [new(0, 0, 0, 0, 0, 1, 1), new(2, 0, 0x40, 0, 0, 1, 1), new(4, 0, 0, 0, 0, 30, 30)], 0x15, 0);
        byte[] script =
        [
            0x13, 0, 0, 0x62, 0x56, 0x34, 0xF2, 0x90,
            0x10, 0x23, 0, 0x60, 1, 0x90,
            0x12, 0x70, 0x20, 0x02, 0xA0, 0x20, 0x00, 0x26, 0x40, 0x80, 0x60, 1, 0x40, 0x82, 0x90,
            0x03, 0, 0, 0x62, 0x56, 0x34, 0x72, 0x44, 0x70, 0x2F, 0,
            0x60, 0x20, 0x61, 0x10, 1, 0x92, 0x73
        ];
        Ff7BattleAiResult result = Ff7BattleAi.Run(script, context, [], formation, random);
        Check(BinaryPrimitives.ReadUInt32LittleEndian(result.LocalMemory) == 0xF23456 && result.LocalMemory[4] == 8, "Native three-byte operand or bit-addressed local store changed.");
        Check(result.Commands.SequenceEqual(new Ff7BattleAiCommand[] { new(4, 0x20, 272, 7, 4) }), "Actor row filter, unsigned comparison, physical scene index or queue target changed.");
        Check(random.Capture().Indices[0] == 1, "Native random selection must consume a byte for a singleton mask.");

        byte[] empty = [0x12, 0x70, 0x20, 0x60, 0, 0x82, 0x90, 0x73];
        Ff7BattleAi.Run(empty, context, result.LocalMemory, formation, random);
        Check(random.Capture().Indices[0] == 1, "Native random selection must consume no byte for an empty mask.");
        byte[] message = [0x93, 0xEA, 0xFF, 0x01, 0x2C, 0xFF, 0x60, 0x24, 0x60, 0, 0x92, 0x73];
        Ff7BattleAiResult messageResult = Ff7BattleAi.Run(message, context, [], formation, random);
        Check(messageResult.Commands.Select(command => command.CommandId).SequenceEqual(new byte[] { 0x21, 0x24 }) && messageResult.Commands[0].Message!.SequenceEqual(message.AsSpan(1, 5).ToArray()), "Battle text control operands and its ordered wait receipt changed.");
        byte[] invalidBranch = [0x72, 0x34, 0x12];
        ExpectInvalid(() => Ff7BattleAi.Run(invalidBranch, context, [], formation, random));
        byte[] invalidLocal = [0x01, 0, 4, 0x73];
        ExpectInvalid(() => Ff7BattleAi.Run(invalidLocal, context, [], formation, random));
        byte[] truncated = [0x62, 0xFF, 0];
        ExpectInvalid(() => Ff7BattleAi.Run(truncated, context, [], formation, random));
    }

    public static void RunInstalled(SceneFormation opening, IReadOnlyList<KernelSection> kernel, Ff7Progress initial)
    {
        SceneBattleData formation = SceneBattleData.Read(opening);
        ReadOnlyMemory<byte> kernelBattle = kernel.Single(section => section.Type == 2).Data;
        ReadOnlyMemory<byte> kernelAttacks = kernel.Single(section => section.Type == 1).Data;
        IReadOnlyList<Ff7NativeEnemyState> enemies = Ff7NativeEnemyInitialization.Create(formation);
        Ff7CharacterState cloud = initial.Characters.Single(character => character.Id == 0);
        Ff7BattleAiActor[] actors = new[] { new Ff7BattleAiActor(0, 0, 0, (ushort)cloud.MagicPoints, (ushort)cloud.MagicPoints, (uint)cloud.HitPoints, (uint)cloud.HitPoints) }.Concat(enemies.Select(enemy => new Ff7BattleAiActor(enemy.ActorSlot, enemy.Statuses, enemy.Conditions, enemy.MagicPoints, enemy.MaxMagicPoints, enemy.HitPoints, enemy.MaxHitPoints, enemy.Defense, enemy.MagicDefense))).ToArray();
        const ushort occupied = 0x31;
        Ff7BattleRng random = new(kernelBattle.Span, Ff7BattleRng.BeginBattle());
        ushort[] gauges = Ff7NativeAtb.Initialize(occupied, opening.Layout, false, random);
        byte dexterity = cloud.NativeRecord[6];
        int denominator = Ff7NativeAtb.SpeedDenominator([dexterity]);
        const byte battleSpeed = 128;
        ushort[] increments = new ushort[10];
        increments[0] = Ff7NativeAtb.Increment(0, dexterity, 0, 0, denominator, battleSpeed);
        foreach (SceneBattleEnemy enemy in formation.Enemies)
        {
            int slot = enemy.FormationSlot + 4;
            increments[slot] = Ff7NativeAtb.Increment(slot, enemy.Speed, 0, 0, denominator, battleSpeed);
        }

        int firstEnemy = -1;
        for (int tick = 0; tick < 1000 && firstEnemy == -1; tick++)
        {
            for (int slot = 0; slot < gauges.Length; slot++)
            {
                bool ready = Ff7NativeAtb.Advance(ref gauges[slot], increments[slot], false, false);
                if (ready && slot >= 4)
                {
                    firstEnemy = slot;
                    break;
                }
            }
        }
        Check(firstEnemy == 4, "Original formation RNG and ATB must make actor slot 4 the first ready enemy.");
        TurnBoundary readyBoundary = new(gauges, random.Capture(), new(), []);
        TurnBoundary first = Execute(readyBoundary, firstEnemy);
        Check(first.Pending.SequenceEqual(new Ff7BattleAiCommand[] { new(4, 0x20, 272, 0, 1) }), "The first original enemy turn did not queue MP4 Machine Gun against Cloud.");
        Check(first.Random.Stream == 3 && first.Random.WordCounter == 4 && first.Random.Indices.SequenceEqual(new byte[] { 44, 22, 12, 7, 2, 1, 0, 0 }), "MP4 script consumed a different original battle RNG stream.");
        Check(first.Locals[4][12] == 2 && BinaryPrimitives.ReadUInt16LittleEndian(first.Locals[4].AsSpan(16)) == 272 && readyBoundary.Locals.Count == 0, "MP4 writes must be captured in its own actor-local bank.");
        Check(JsonSerializer.Serialize(Execute(Restore(readyBoundary), firstEnemy)) == JsonSerializer.Serialize(first), "Cold ready-state restore changed the native enemy queue publication.");

        TurnBoundary second = Execute(first, 5);
        Check(second.Pending.SequenceEqual(new Ff7BattleAiCommand[] { new(4, 0x20, 272, 0, 1), new(5, 0x20, 273, 1, 1) }), "The other original MP must queue its own action while the first receipt remains stable.");
        Check(second.Locals[4].SequenceEqual(first.Locals[4]) && BinaryPrimitives.ReadUInt16LittleEndian(second.Locals[5].AsSpan(16)) == 273 && second.Random.Stream == 4 && second.Random.WordCounter == 5 && second.Random.Indices.SequenceEqual(new byte[] { 44, 22, 12, 8, 4, 1, 0, 0 }), "The second actor's local memory or shared RNG changed.");
        Check(JsonSerializer.Serialize(Execute(Restore(first), 5)) == JsonSerializer.Serialize(second), "Cold queued-state restore changed the next native AI callback or replayed a completed callback.");

        TurnBoundary Execute(TurnBoundary state, int slot)
        {
            Ff7BattleRng working = new(kernelBattle.Span, state.Random);
            SceneBattleEnemy enemy = formation.Enemies.Single(candidate => candidate.FormationSlot + 4 == slot);
            Ff7BattleAiResult turn = Ff7BattleAi.Run(enemy.Scripts![1].Span, new(slot, actors, occupied, 0), state.Locals.GetValueOrDefault(slot), formation, working, kernelAttacks);
            Dictionary<int, byte[]> locals = new(state.Locals) { [slot] = turn.LocalMemory };
            return state with { Random = working.Capture(), Locals = locals, Pending = [.. state.Pending, .. turn.Commands] };
        }
    }

    public static void RunInstalledMatrix(IReadOnlyList<SceneFormation> formations, IReadOnlyList<KernelSection> kernel, Ff7Progress initial)
    {
        int[] selected = [.. Enumerable.Range(300, 25), 328, 329, 330, .. Enumerable.Range(332, 28), 364, 367, .. Enumerable.Range(392, 4)];
        ReadOnlyMemory<byte> kernelBattle = kernel.Single(section => section.Type == 2).Data;
        ReadOnlyMemory<byte> kernelAttacks = kernel.Single(section => section.Type == 1).Data;
        int turns = 0;
        foreach (int id in selected)
        {
            SceneBattleData formation = SceneBattleData.Read(formations.Single(item => item.FormationId == id));
            Check(formation.FormationScripts is null, $"Formation {id} unexpectedly requires a formation-level script owner.");
            IReadOnlyList<Ff7NativeEnemyState> enemies = Ff7NativeEnemyInitialization.Create(formation);
            Dictionary<int, Ff7BattleAiActor> actors = new();
            int partyCount = id < 324 ? 1 : id < 364 ? 2 : 3;
            for (int slot = 0; slot < partyCount; slot++)
            {
                Ff7CharacterState character = initial.Characters.Single(character => character.Id == slot);
                actors.Add(slot, new(slot, 0, 0, (ushort)character.MagicPoints, (ushort)character.MagicPoints, (uint)character.HitPoints, (uint)character.HitPoints));
            }
            foreach (Ff7NativeEnemyState enemy in enemies)
            {
                actors.Add(enemy.ActorSlot, new(enemy.ActorSlot, enemy.Statuses, enemy.Conditions, enemy.MagicPoints, enemy.MaxMagicPoints, enemy.HitPoints, enemy.MaxHitPoints, enemy.Defense, enemy.MagicDefense));
            }
            ushort eligible = (ushort)actors.Keys.Sum(slot => 1 << slot);
            Ff7BattleRng random = new(kernelBattle.Span, Ff7BattleRng.BeginBattle());
            Dictionary<int, byte[]> locals = new();
            foreach (SceneBattleEnemy enemy in formation.Enemies)
            {
                Execute(enemy, 0);
            }
            foreach (SceneBattleEnemy enemy in formation.Enemies)
            {
                Execute(enemy, 1);
                turns++;
            }

            void Execute(SceneBattleEnemy enemy, int callback)
            {
                int slot = enemy.FormationSlot + 4;
                try
                {
                    Ff7BattleAiContext context = new(slot, actors.Values.ToArray(), eligible, 0, Callback: (byte)callback);
                    Ff7BattleAiResult result = Ff7BattleAi.Run(enemy.Scripts is null ? [] : enemy.Scripts[callback].Span, context, locals.GetValueOrDefault(slot), formation, random, kernelAttacks);
                    locals[slot] = result.LocalMemory;
                    foreach (Ff7BattleAiActor actor in result.ActorUpdates)
                    {
                        actors[actor.Slot] = actor;
                    }
                    foreach (Ff7BattleAiCommand command in result.Commands)
                    {
                        Check(command.ActorSlot == slot && (command.TargetMask & ~eligible) == 0, $"Formation {id} issued a command for an absent actor slot.");
                        if (command.CommandId == 0x20)
                        {
                            Check(formation.Attacks.Any(attack => attack.Slot == command.SceneAttackSlot && attack.Id == command.AttackId), $"Formation {id} lost its native action selector.");
                        }
                    }
                    if (id == 324 && callback == 0)
                    {
                        Check(enemies.Single(item => item.ActorSlot == slot).MagicDefense == 300 && actors[slot].MagicDefense == 256 && BinaryPrimitives.ReadUInt16LittleEndian(result.LocalMemory.AsSpan(4)) == 256 && BinaryPrimitives.ReadUInt16LittleEndian(result.LocalMemory.AsSpan(8)) == 40, "Guard Scorpion's initialization must publish the defense override before reading it into local memory.");
                    }
                    if (id == 364 && callback == 0)
                    {
                        Check(actors[slot].MagicDefense == 320 && (actors[slot].Conditions & 0x1000) != 0, "Air Buster's initialization must publish its original condition and magic defense writes.");
                    }
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException($"Formation {id}, actor slot {slot}, native enemy {enemy.Placement.EnemyId}, AI callback {callback} failed.", exception);
                }
            }
        }
        Check(turns == 180, $"The opening route matrix must cover 180 enemy occurrences. Found {turns}.");
        RunGuardScorpion(formations.Single(formation => formation.FormationId == 324), kernelBattle, kernelAttacks);
        RunAirBuster(formations.Single(formation => formation.FormationId == 364), kernelBattle, kernelAttacks);
    }

    private static void RunGuardScorpion(SceneFormation scene, ReadOnlyMemory<byte> kernelBattle, ReadOnlyMemory<byte> kernelAttacks)
    {
        SceneBattleData formation = SceneBattleData.Read(scene);
        IReadOnlyList<ReadOnlyMemory<byte>> scripts = formation.Enemies.Single().Scripts!;
        Ff7BattleAiActor[] actors = [new(0, 0, 0, 0, 0, 300, 300, Variant: 16), new(1, 0, 0, 0, 0, 300, 300, Variant: 17), new(4, 0, 31, 0, 0, 800, 800, 40, 300)];
        Ff7BattleRng random = new(kernelBattle.Span, Ff7BattleRng.BeginBattle());
        Ff7BattleAiResult initialized = Execute(0, []);
        Ff7BattleAiResult current = initialized;
        (int SceneSlot, ushort TargetMask, int CommandCount)[] native = [(4, 1, 2), (2, 1, 1), (4, 2, 2), (3, 2, 1), (7, 16, 7)];
        for (int turn = 0; turn < native.Length; turn++)
        {
            current = Execute(1, current.LocalMemory);
            Check(current.Commands.Length == native[turn].CommandCount && current.Commands[0].SceneAttackSlot == native[turn].SceneSlot && current.Commands[0].TargetMask == native[turn].TargetMask, $"Guard Scorpion native turn {turn + 1} queue changed.");
        }
        Check(actors[2] is { IdleAction: 7, HurtAction: 8, Defense: 255, MagicDefense: 384 } && current.Commands.Select(command => command.CommandId).SequenceEqual(new byte[] { 0x20, 0x21, 0x24, 0x21, 0x24, 0x21, 0x24 }), "Guard Scorpion's fifth turn must publish its native tail pose, defense and ordered text/wait commands.");
        Ff7BattleRngState savedRandom = random.Capture();
        Check(savedRandom.Stream == 1 && savedRandom.WordCounter == 2 && savedRandom.Indices.SequenceEqual(new byte[] { 46, 21, 10, 5, 2, 1, 0, 0 }), "Guard Scorpion's original turn sequence consumed different randomness.");
        Ff7BattleAiResult counter = Execute(2, current.LocalMemory);
        Check(counter.Commands.Length == 1 && counter.Commands[0] is { CommandId: 0x20, AttackId: 0x11E, TargetMask: 3, Callback: 2 }, "Guard Scorpion's tail-up general counter must target both original party slots.");

        Ff7BattleAiResult Execute(int callback, ReadOnlySpan<byte> local)
        {
            Ff7BattleAiResult result = Ff7BattleAi.Run(scripts[callback].Span, new(4, actors, 0x13, 0, Callback: (byte)callback), local, formation, random, kernelAttacks);
            foreach (Ff7BattleAiActor update in result.ActorUpdates)
            {
                actors[Array.FindIndex(actors, actor => actor.Slot == update.Slot)] = update;
            }
            return result;
        }
    }

    private static void RunAirBuster(SceneFormation scene, ReadOnlyMemory<byte> kernelBattle, ReadOnlyMemory<byte> kernelAttacks)
    {
        SceneBattleData formation = SceneBattleData.Read(scene);
        IReadOnlyList<ReadOnlyMemory<byte>> scripts = formation.Enemies.Single().Scripts!;
        Ff7BattleAiActor[] actors = [new(0, 0, 2, 0, 0, 300, 300), new(1, 0, 0, 0, 0, 300, 300), new(2, 0, 0, 0, 0, 300, 300), new(4, 0, 31, 0, 0, 1200, 1200, 80, 320)];
        BossBoundary initial = new(actors, [], Ff7BattleRng.BeginBattle(), []);
        BossBoundary first = Execute(Execute(initial, 0), 1);
        Check(first.Commands.Select(command => (command.CommandId, command.SceneAttackSlot, command.TargetMask)).SequenceEqual(new (byte, int, ushort)[] { (0x21, -1, 7), (0x20, 10, 16), (0x20, 12, 4) }), "Air Buster's first native main callback must publish the announcement, self-turn and opposing attack in order.");

        Ff7BattleAiActor[] frontHit = (Ff7BattleAiActor[])first.Actors.Clone();
        frontHit[^1] = frontHit[^1] with { HitPoints = 150, LastAttackerMask = 1 };
        BossBoundary front = Execute(first with { Actors = frontHit }, 2);
        Check(front.Commands is [{ CommandId: 0x21, Callback: 2 }] && front.Actors[^1] is { IdleAction: 10, HurtAction: 1 }, "Air Buster's low-HP front-side counter must issue the native turn-function warning only.");

        Ff7BattleAiActor[] rearHit = (Ff7BattleAiActor[])front.Actors.Clone();
        rearHit[^1] = rearHit[^1] with { LastAttackerMask = 4 };
        BossBoundary beforeRear = front with { Actors = rearHit };
        BossBoundary rear = Execute(beforeRear, 2);
        Check(rear.Commands is [{ CommandId: 0x21, Callback: 2 }, { CommandId: 0x20, SceneAttackSlot: 4, TargetMask: 4, Callback: 2 }] && rear.Actors[^1] is { IdleAction: 10, HurtAction: 3 }, "Air Buster must retain its side-specific hurt pose and counter only the last attacker.");
        BossBoundary restored = JsonSerializer.Deserialize<BossBoundary>(JsonSerializer.Serialize(beforeRear))!;
        Check(JsonSerializer.Serialize(Execute(restored, 2)) == JsonSerializer.Serialize(rear), "Air Buster's rear counter changed after restoring its live HP, pose, locals, last attacker and RNG.");

        BossBoundary main = Execute(rear, 1);
        BossBoundary death = Execute(main, 3);
        Check(death.Commands is [{ CommandId: 0x20, SceneAttackSlot: 6, TargetMask: 16, Callback: 3 }] && death.Random.Indices.SequenceEqual(new byte[] { 43, 20, 10, 5, 2, 1, 0, 0 }) && death.Random.WordCounter == 0, "Air Buster's native death callback or byte-only RNG continuation changed.");

        BossBoundary Execute(BossBoundary state, byte callback)
        {
            Ff7BattleRng working = new(kernelBattle.Span, state.Random);
            Ff7BattleAiResult result = Ff7BattleAi.Run(scripts[callback].Span, new(4, state.Actors, 0x17, 0, Callback: callback), state.Locals, formation, working, kernelAttacks);
            Ff7BattleAiActor[] updated = state.Actors.Select(actor => result.ActorUpdates.SingleOrDefault(item => item.Slot == actor.Slot) ?? actor).ToArray();
            return new(updated, result.LocalMemory, working.Capture(), result.Commands);
        }
    }

    private static TurnBoundary Restore(TurnBoundary state) => JsonSerializer.Deserialize<TurnBoundary>(JsonSerializer.Serialize(state))!;

    private static void ExpectInvalid(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new Exception("Invalid FFVII battle AI was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
