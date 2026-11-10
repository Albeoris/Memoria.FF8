using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class InstalledOpeningTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed opening: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        var entry = Ff7FieldResources.Load(installation, "md1stin").Triggers.Gateways.Single(gateway => gateway.DestinationField == 117);
        Equal(58, entry.Direction);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Session? cold = null;
        try
        {
            for (int tick = 0; tick < 4500; tick++)
            {
                Step(original, tick);
                if (cold is not null)
                {
                    Step(cold, tick);
                    string expected = JsonSerializer.Serialize(original.Capture());
                    string actual = JsonSerializer.Serialize(cold.Capture());
                    if (expected != actual)
                    {
                        int difference = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).First(index => expected[index] != actual[index]);
                        throw new Exception($"Cold opening mismatch tick {tick}, JSON offset {difference}: {expected.Substring(Math.Max(0, difference - 40), Math.Min(120, expected.Length - Math.Max(0, difference - 40)))} versus {actual.Substring(Math.Max(0, difference - 40), Math.Min(120, actual.Length - Math.Max(0, difference - 40)))}");
                    }
                }
                if (tick is 3740 or 4000 or 4262 or 4264)
                {
                    cold?.Dispose();
                    Ff7SessionSave save = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(original.Capture()))!;
                    cold = Ff7Session.Restore(installation, save);
                }
            }
            Equal("md1stin", original.Field.Name);
            Equal(false, original.Host.Process.PlayerLocked);
            Equal(false, original.CanSave);
            Equal((byte)2, (byte)(original.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 31) & 2));
            Equal(1, original.Progress.CompletedBattles.Length);
            Equal((ushort)300, original.Progress.CompletedBattles[0].Formation);
            Equal(7, original.Progress.Characters[0].Level);
            Equal(0, original.Host.Process.BattleResult);
            Equal(0, original.Host.Process.Actors.Single(actor => actor.PlayerControlled).PartyCharacter);
            Equal(true, original.Host.Process.Movie is { Completed: true });
            Equal(2, original.Host.Process.Actors.Count(actor => actor.Entity == 5));
            Equal(1, original.Host.Process.Actors.Count(actor => actor.Entity == 5 && actor.EntityBound));
            original.Capture().Validate();
        }
        finally
        {
            cold?.Dispose();
        }
    }

    public static void RunDeferredBattle()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed deferred opening battle: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        original.SetRandomEncountersEnabled(false);
        FieldHostRequest? started = null;
        original.Host.BeginBattleRequest = request =>
        {
            Equal<FieldHostRequest?>(null, started);
            started = request;
            return new(false);
        };
        for (int tick = 0; tick < 4500 && started is null; tick++)
        {
            Step(original, tick);
        }
        Equal((ushort)300, (started?.Command as FieldBattleCommand)?.Formation ?? ushort.MaxValue);
        long requestId = started!.Id;
        Ff7SessionSave checkpoint = original.Capture();
        Equal(true, original.Host.HasUnfinishedBattle);
        Equal(false, original.CanSave);
        Equal(0, original.Progress.CompletedBattles.Length);
        Equal(true, checkpoint.Vm.PendingRequests!.Any(request => request.Id == requestId));
        string saved = JsonSerializer.Serialize(checkpoint);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(saved)!);
        Equal(false, cold.Progress.RandomEncountersEnabled);

        JsonObject legacy = JsonNode.Parse(saved)!.AsObject();
        legacy[nameof(Ff7SessionSave.Progress)]!.AsObject().Remove(nameof(Ff7Progress.RandomEncountersEnabled));
        legacy[nameof(Ff7SessionSave.Progress)]!.AsObject().Remove(nameof(Ff7Progress.FullCombatEnabled));
        using Ff7Session legacyCold = Ff7Session.Restore(installation, legacy.Deserialize<Ff7SessionSave>()!);
        Equal(true, legacyCold.Progress.RandomEncountersEnabled);
        Equal(false, legacyCold.Progress.FullCombatEnabled);

        original.AdvanceTick();
        cold.AdvanceTick();
        Equal(saved, JsonSerializer.Serialize(original.Capture()));
        Equal(saved, JsonSerializer.Serialize(cold.Capture()));
        RunFirstEnemyAction(installation, cold.Progress, (FieldBattleCommand)started.Command);
        Equal(saved, JsonSerializer.Serialize(cold.Capture()));
        original.Host.PublishBattleResult(requestId, 0);
        cold.Host.PublishBattleResult(requestId, 0);
        original.AdvanceTick();
        cold.AdvanceTick();
        Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Equal(false, cold.Host.HasUnfinishedBattle);
        Equal(false, (cold.Machine.Capture().PendingRequests ?? []).Any(request => request.Id == requestId));
        Equal(0, cold.Progress.CompletedBattles.Length);
        Console.WriteLine("Original formation 300 held its field request over a cold restore and consumed the test completion once.");
    }

    public static void RunPlayableBattle()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed playable opening battle: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        original.SetRandomEncountersEnabled(false);
        original.SetFullCombatEnabled(true);
        for (int tick = 0; tick < 4500 && original.Battle is null; tick++)
        {
            Step(original, tick);
        }
        Ff7BattleSession battle = original.Battle ?? throw new Exception("Original opening script failed to start combat.");
        Equal(300, battle.Formation.FormationId);
        Equal(true, original.Host.HasUnfinishedBattle);
        Equal(false, original.CanSave);
        long fieldTick = original.Machine.Capture().Tick;
        int startHp = original.Progress.Characters[0].HitPoints;
        for (int tick = 0; tick < 1000 && battle.LastHit is null; tick++)
        {
            original.AdvanceTick();
        }
        Ff7BattleHit firstHit = battle.LastHit ?? throw new Exception("Opening enemy did not land its first action.");
        Equal(4, firstHit.Source);
        Equal(0, firstHit.Target);
        Equal((ushort)273, firstHit.AttackId);
        Equal(false, firstHit.Damage.Missed);
        Equal(7, firstHit.Damage.Damage);
        Equal((uint)(startHp - firstHit.Damage.Damage), battle.Actors[0].Actor.HitPoints);
        Equal(fieldTick, original.Machine.Capture().Tick);
        string saved = JsonSerializer.Serialize(original.Capture());
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(saved)!);
        bool approached = false;
        for (int tick = 0; tick < 6000 && battle.Outcome == Ff7BattleOutcome.Fighting; tick++)
        {
            SubmitAttack(battle);
            SubmitAttack(cold.Battle!);
            original.AdvanceTick();
            cold.AdvanceTick();
            approached |= battle.Action is { Command.ActorSlot: 0, Motion: { Remaining: > 0 } };
            if (battle.LastHit?.Tick == battle.Tick || tick % 100 == 0)
            {
                Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(cold.Capture()));
            }
        }
        Equal(Ff7BattleOutcome.Victory, battle.Outcome);
        Equal(true, approached);
        Equal(0, original.Progress.CompletedBattles.Length);
        Equal(true, original.ConfirmBattleVictory());
        Equal(true, cold.ConfirmBattleVictory());
        Equal(false, original.ConfirmBattleVictory());
        original.AdvanceTick();
        cold.AdvanceTick();
        Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Equal(false, original.Host.HasUnfinishedBattle);
        Equal(1, original.Progress.CompletedBattles.Length);
        Equal((ushort)300, original.Progress.CompletedBattles[0].Formation);
        Console.WriteLine($"Original BATTLE 300 took native attack {firstHit.AttackId} for {firstHit.Damage.Damage} HP, restored cold and published one victory.");
        CheckEncounterEscape(original);

        static void SubmitAttack(Ff7BattleSession battle)
        {
            if (battle.ReadyPlayer is int slot)
            {
                Ff7BattleCommandOption attack = battle.Commands(slot).Single(command => command.Command == Ff7BattleCommand.Attack);
                int target = battle.Actors.First(actor => actor.Actor.Slot >= 4 && (attack.ValidTargets & 1 << actor.Actor.Slot) != 0).Actor.Slot;
                Equal(true, battle.Submit(slot, attack.Command, attack.Argument, (ushort)(1 << target)));
            }
        }
    }

    private static void CheckEncounterEscape(Ff7Session session)
    {
        long request = session.Progress.NextEncounterRequestId;
        int entity = session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Entity;
        session.Host.StartEncounter(new(request, entity, 7, 0, new FieldBattleCommand(301)), 0);
        Ff7BattleSession encounter = session.Battle ?? throw new InvalidOperationException("The field encounter did not start a native battle.");
        for (int tick = 0; tick < 6000 && encounter.Outcome == Ff7BattleOutcome.Fighting; tick++)
        {
            session.AdvanceTick(advanceClock: false, escapeBattleHeld: true);
        }
        Equal(Ff7BattleOutcome.Escaped, encounter.Outcome);
        Equal(true, session.ConfirmBattleEscape());
        Equal(false, session.ConfirmBattleEscape());
        Equal(8, session.Host.Process.BattleResult);
        Equal((ushort)1, session.Progress.BattleEscapes);
        Equal(1, session.Progress.CompletedBattles.Length);
        session.AdvanceTick();
        Equal<FieldHostRequest?>(null, session.Host.Process.EncounterRequest);
        Console.WriteLine("The original formation 301 field owner consumed one flee result without awarding victory rewards.");
    }

    private static void RunFirstEnemyAction(Ff7Installation installation, Ff7Progress progress, FieldBattleCommand request)
    {
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation source = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == request.Formation);
        SceneBattleData formation = SceneBattleData.Read(source);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7CharacterState cloud = progress.Characters.Single(character => character.Id == progress.Party[0]);
        Ff7BattlePlayerStats stats = Ff7NativeBattlePlayerStats.Create(cloud, kernel, executable.NativeMateriaStatModifiers);
        Ff7Progress party = Ff7NativePartyRecovery.Publish(progress, Ff7MateriaResources.LoadRecords(installation), executable.NativeMateriaStatModifiers).Progress;
        ReadOnlySpan<byte> record = party.Characters.Single(character => character.Id == cloud.Id).NativeRecord;
        Ff7BattleAiActor player = new(0, stats.Actor.Statuses, stats.Actor.BackRow ? 0x48u : 8u, (ushort)cloud.MagicPoints, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(record[58..]), (uint)cloud.HitPoints, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(record[56..]), stats.Actor.Defense, stats.Actor.MagicDefense);
        Ff7BattleAiActor[] actors = [player, .. Ff7NativeEnemyInitialization.Create(formation).Select(enemy => new Ff7BattleAiActor(enemy.ActorSlot, enemy.Statuses, enemy.Conditions, enemy.MagicPoints, enemy.MaxMagicPoints, enemy.HitPoints, enemy.MaxHitPoints, enemy.Defense, enemy.MagicDefense))];
        const ushort mask = 0x31;
        ReadOnlySpan<byte> battleKernel = kernel.Single(section => section.Type == 2).Data.Span;
        Ff7BattleRng random = new(battleKernel, Ff7BattleRng.BeginBattle(progress.BattleRng?.CrtSeed ?? 1));
        ushort[] gauges = Ff7NativeAtb.Initialize(mask, source.Layout, false, random);
        int denominator = Ff7NativeAtb.SpeedDenominator([cloud.NativeRecord[6]]);
        ushort[] increments = new ushort[Ff7NativeAtb.SlotCount];
        increments[0] = Ff7NativeAtb.Increment(0, stats.Actor.Dexterity, stats.Actor.DexterityModifier, stats.Actor.Statuses, denominator, 128);
        foreach (SceneBattleEnemy candidate in formation.Enemies)
        {
            increments[candidate.FormationSlot + 4] = Ff7NativeAtb.Increment(candidate.FormationSlot + 4, candidate.Speed, 0, 0, denominator, 128);
        }
        int firstEnemy = -1;
        for (int tick = 0; tick < 1000 && firstEnemy < 0; tick++)
        {
            foreach (Ff7BattleAiActor actor in actors)
            {
                if (Ff7NativeAtb.Advance(ref gauges[actor.Slot], increments[actor.Slot], false, false) && actor.Slot >= 4)
                {
                    firstEnemy = actor.Slot;
                    break;
                }
            }
        }
        Equal(4, firstEnemy);
        SceneBattleEnemy enemy = formation.Enemies.Single(candidate => candidate.FormationSlot + 4 == firstEnemy);
        Ff7BattleAiCommand action = Ff7BattleAi.Run(enemy.Scripts![1].Span, new(firstEnemy, actors, mask, 0), [], formation, random).Commands.Single();
        Equal((ushort)272, action.AttackId);
        Equal((ushort)1, action.TargetMask);
        Ff7BattleAttack attack = formation.Attacks.Single(candidate => candidate.Slot == action.SceneAttackSlot).Data;
        Ff7BattleDamageResult damage = Ff7NativeBattleDamage.Resolve(Ff7NativeBattleDamage.FromEnemy(enemy), stats.Actor, Ff7BattleDamageAction.FromAttack(attack), random);

        Equal(new Ff7BattleDamageResult(false, false, 4), damage);
        Ff7BattleRngState afterHit = random.Capture();
        Equal(5, afterHit.Stream);
        Equal(6u, afterHit.WordCounter);
        Equal(true, afterHit.Indices.SequenceEqual(new byte[] { 44, 22, 12, 9, 4, 3, 0, 0 }));

        int animation = enemy.Attacks.Single(candidate => candidate.AttackId == action.AttackId).ActionAnimation;
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        byte[] ReadModel(string name) => models.ReadEntry(models.Find(name).Single());
        BattleModelData model = BattleModelData.Read(ReadModel("aqaa"), ReadModel("aqda"));
        BattleActionBank bank = BattleActionBank.Read(ReadModel("aqab"));
        BattleActionInstruction trigger = BattleActionInstructions.ReadUntilIdle(bank.Scripts[animation], model.BodyClips.Count).Single(instruction => instruction.Opcode == 0xF7);
        Ff7BattleTargetCallbacks reaction = Ff7BattleTargetCallbacks.Schedule(action.TargetMask, trigger.Operands.Span[0]);
        for (int callback = 0; callback < reaction.Delay; callback++)
        {
            reaction = reaction.Advance(false, out int? premature);
            Equal<int?>(null, premature);
        }
        Ff7BattleTargetCallbacks restored = JsonSerializer.Deserialize<Ff7BattleTargetCallbacks>(JsonSerializer.Serialize(reaction))!;
        Ff7BattleTargetCallbacks completed = reaction.Advance(false, out int? target);
        Equal<int?>(0, target);
        Equal(completed, restored.Advance(false, out int? coldTarget));
        Equal(target, coldTarget);
        Equal((ushort)0, completed.RemainingTargets);
        Console.WriteLine($"Native request 300 first enemy action: slot {firstEnemy}, attack {action.AttackId}, HP delta {-damage.Damage}, reaction delay {reaction.Delay}.");
    }

    private static void Step(Ff7Session session, int tick)
    {
        if (session.Host.Process.Movie is { Playing: true } movie)
        {
            session.Host.PublishMovieFrame(Math.Min(movie.Frame + (tick % 2), 1799), movie.Frame >= 1799);
        }
        if (tick % 30 == 0)
        {
            foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
            {
                session.Host.CompleteDialogue(dialogue.RequestId, dialogue.FirstChoice ?? 0);
            }
        }
        if (tick >= 4110 && !session.Host.Process.PlayerLocked)
        {
            session.MovePlayer(0, -1, true);
        }
        session.AdvanceTick();
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
