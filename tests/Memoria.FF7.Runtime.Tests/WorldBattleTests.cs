using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldBattleTests
{
    public static void Run()
    {
        TestSelection();
        WorldEncounterModifierTests.Run();
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed world battle: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session field = Ff7Session.NewGame(installation);
        Ff7SessionSave fieldSave = field.Capture();
        byte[] variables = fieldSave.Vm.PersistentVariables.ToArray();
        variables[0] = 152;
        byte[] party = variables.AsSpan(265, 3).ToArray();
        using Ff7WorldRuntime fixture = new(installation, variables, 1, party);
        fixture.Advance();
        Ff7WorldProcess initial = fixture.Process;
        using Ff7Session original = Ff7Session.Restore(installation, fieldSave with { Vm = fieldSave.Vm with { PersistentVariables = variables }, World = initial });
        original.SetFullCombatEnabled(true);
        original.SetRandomEncountersEnabled(false);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        byte[] source = resources.ReadRequired("enc_w.bin");
        SceneFormation[] formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).ToArray();
        ushort formation = (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(0xA2)) & 1023);
        Check(formations.Any(row => row.FormationId == formation), "Original Grasslands encounter table must name a loaded formation.");
        Ff7WorldRuntime world = original.World!;
        world.Execute(new(initial.Vm.NextRequestId, -1, -1, 0, new(WorldOperation.Battle, [formation])));
        Check(world.PendingBattle is { Scripted: true }, "Disabling random encounters blocked a scripted battle.");
        using Ff7Session coldEntry = Ff7Session.Restore(installation, RoundTrip(original));
        original.AdvanceTick(false);
        coldEntry.AdvanceTick(false);
        Check(original.Battle?.Formation.FormationId == formation && !original.CanSave && !original.CanSaveMemoria, "World occurrence did not own its battle or enabled player saving.");
        Check(Snapshot(original) == Snapshot(coldEntry), "Cold pending world occurrence selected a different battle state.");
        string frozenWorld = JsonSerializer.Serialize(world.Process);
        original.AdvanceTick(false);
        coldEntry.AdvanceTick(false);
        Check(JsonSerializer.Serialize(world.Process) == frozenWorld && Snapshot(original) == Snapshot(coldEntry), "Battle time advanced the suspended world or changed cold battle continuation.");

        Ff7SessionSave during = RoundTrip(original);
        using Ff7Session coldBattle = Ff7Session.Restore(installation, during);
        original.AdvanceTick(false);
        coldBattle.AdvanceTick(false);
        Check(Snapshot(original) == Snapshot(coldBattle), "Restoring a diagnostic world battle changed its next logical callback.");

        Ff7BattleSnapshot escaped = during.Battle! with { Outcome = Ff7BattleOutcome.Escaped, TerminalFrames = 45 };
        using Ff7Session returning = Ff7Session.Restore(installation, during with { Battle = escaped });
        for (int callback = 0; callback < 300 && !returning.Battle!.ResultReady; callback++)
        {
            returning.AdvanceTick(false);
        }
        Check(returning.ConfirmBattleEscape(), "World escape did not restore its exact suspended world.");
        Check(!returning.ConfirmBattleEscape() && returning.World!.PendingBattle is null && returning.Progress.BattleEscapes == 1, "Escape return published twice or kept the occurrence pending.");
        Ff7WorldBattleResultReceipt resultReceipt = returning.Progress.WorldBattleResults.Single();
        Check(resultReceipt == new Ff7WorldBattleResultReceipt(during.FieldGeneration, escaped.RequestId, escaped.FormationId, Ff7BattleOutcome.Escaped), "World escape did not commit its exact encounter result identity.");
        TestGuestSettlement(returning.Capture(), resultReceipt);
        Reject(() => (during with { Progress = during.Progress with { WorldBattleResults = [resultReceipt] } }).Validate());
        Reject(() => (during with { Battle = null, Progress = during.Progress with { WorldBattleResults = [resultReceipt] } }).Validate());
        Ff7WorldProcess resumed = returning.World!.Process;
        Check(JsonSerializer.Serialize(resumed.Entities) == JsonSerializer.Serialize(initial.Entities) && resumed.Tick == initial.Tick && resumed.Vm.System == initial.Vm.System with { Calls = resumed.Vm.System.Calls }, "World escape changed the retained actor placement or system PC.");
        using Ff7Session restored = Ff7Session.Restore(installation, RoundTrip(returning));
        Check(Snapshot(restored) == Snapshot(returning), "Cold restore after battle return replayed the world occurrence.");
        Check(restored.Progress.WorldBattleResults.Single() == resultReceipt && !restored.ConfirmBattleEscape(), "Cold world return lost or repeated its result receipt.");

        Ff7InventorySlot[] fullPotionStack = [new(0, 99, 0)];
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7BattleSnapshot victory = during.Battle! with
        {
            Outcome = Ff7BattleOutcome.Victory,
            TerminalFrames = 45,
            Inventory = fullPotionStack,
            Economy = during.Battle.Economy! with { Inventory = Ff7NativeBattleInventory.Create(fullPotionStack, kernel) },
            Actors = during.Battle.Actors.Select(actor => actor.Actor.Slot >= 4
                ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | Ff7NativeBattleActionResolver.Death } } : actor).ToArray()
        };
        using Ff7Session winning = Ff7Session.Restore(installation, during with { Battle = victory });
        for (int callback = 0; callback < 300 && !winning.Battle!.ResultReady; callback++)
        {
            winning.AdvanceTick(false);
        }
        string beforePreview = Snapshot(winning);
        Ff7VictoryProjection preview = winning.PreviewBattleVictory();
        Ff7VictoryProjection repeated = winning.PreviewBattleVictory();
        Check(beforePreview == Snapshot(winning) && JsonSerializer.Serialize(preview) == JsonSerializer.Serialize(repeated), "World result preview changed live progression or RNG.");
        Check(winning.ConfirmBattleVictory(), "World victory did not restore the suspended world.");
        Check(!winning.ConfirmBattleVictory() && winning.World!.PendingBattle is null, "World victory settled more than once.");
        Check(JsonSerializer.Serialize(winning.Progress) == JsonSerializer.Serialize(preview.Progress), "Confirmed progression differed from the complete reward projection.");

        Ff7WorldBattleState beforeSelection = new(16, 1 << 18, -1, new byte[0x209], 0, null);
        Ff7WorldProcess ready = initial with
        {
            MainState = 1, FadeRate = 0, FadeAlpha = 0, FadeDisplayedAlpha = 0, MovementEnabled = true, ControlLock = 0,
            BattleState = beforeSelection
        };
        using Ff7Session walking = Ff7Session.Restore(installation, fieldSave with { Vm = fieldSave.Vm with { PersistentVariables = variables }, World = ready });
        walking.SetFullCombatEnabled(true);
        walking.SetRandomEncountersEnabled(true);
        walking.World!.SetMovement(1, 0, true);
        using Ff7Session coldSelection = Ff7Session.Restore(installation, RoundTrip(walking));
        walking.AdvanceTick(false);
        coldSelection.AdvanceTick(false);
        Check(walking.World.PendingBattle is { Scripted: false } && walking.Battle is not null, "Original physical movement and terrain did not select a random world encounter.");
        Check(Snapshot(walking) == Snapshot(coldSelection), "Cold movement produced different world encounter or RNG owners.");

        WorldEncounterModifierTests.RunSession(installation, fieldSave, variables, ready);
        TestSpecialReturns(installation, fieldSave, variables, ready, source);
        TestChocoboCapture(installation, fieldSave);
    }

    private static void TestChocoboCapture(Ff7Installation installation, Ff7SessionSave fieldSave)
    {
        byte[] persistent = fieldSave.Vm.PersistentVariables.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(persistent, 386);
        BinaryPrimitives.WriteUInt32LittleEndian(persistent.AsSpan(0x3B8), 235178);
        BinaryPrimitives.WriteUInt32LittleEndian(persistent.AsSpan(0x3BC), 139946);
        persistent[0x3FF] = unchecked((byte)-persistent.Skip(0x380).Take(127).Sum(value => value));
        byte[] party = persistent.AsSpan(0x109, 3).ToArray();
        using Ff7WorldRuntime fixture = new(installation, persistent, 0, party, entryModule: 1);
        for (int step = 0; step < 20; step++)
        {
            fixture.Advance();
        }
        Ff7WorldProcess world = fixture.Process with
        {
            MainState = 1, MovementEnabled = true, ControlLock = 0,
            BattleState = new(16, 1 << 18, -1, new byte[0x209], 0, null)
        };
        Ff7CharacterState[] characters = fieldSave.Progress.Characters.Select(character =>
        {
            if (character.Id != 0)
            {
                return character;
            }
            byte[] record = character.NativeRecord.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 9);
            record[2] = 230;
            return character with { NativeRecord = record };
        }).ToArray();
        using Ff7Session session = Ff7Session.Restore(installation, fieldSave with
        {
            World = world,
            Vm = fieldSave.Vm with { PersistentVariables = persistent },
            Progress = fieldSave.Progress with { Characters = characters, FullCombatEnabled = true }
        });
        session.World!.SetMovement(1, 0, true);
        session.AdvanceTick(false);
        Check(session.World.PendingBattle is { Chocobo: true, Formation: 56 } && session.World.Process.Audio?.Song == 5 && session.World.ReadSpecial(9, -1) == 7, $"Original track did not select the Chocobo formation and rating. Terrain={session.World.Process.Entities.First(entity => entity.Model == 0).Terrain:X4}, battle={session.World.PendingBattle}, movement={session.World.MovementEnabled}.");
        using Ff7Session cold = Ff7Session.Restore(installation, RoundTrip(session));
        Check(Snapshot(session) == Snapshot(cold), "Cold Chocobo encounter changed the battle and world owners.");
        for (int tick = 0; tick < 1800 && !cold.Battle!.ResultReady; tick++)
        {
            cold.AdvanceTick(false, escapeBattleHeld: true);
        }
        Check(cold.Battle is { Outcome: Ff7BattleOutcome.Escaped, ResultReady: true } && cold.ConfirmBattleEscape() && cold.World!.ReadSpecial(10, -1) == 1, "Escaping a Chocobo battle lost its native World return status.");
        cold.AdvanceTick(false);
        Check(cold.World!.ControlledModel == 0 && cold.World.Process.Chocobo is null, "Escaping a Chocobo battle must leave the party on foot.");
        Ff7BattleSession battle = session.Battle!;
        int bird = battle.Data.Enemies.Single(enemy => enemy.Rewards.EnemyId == 339).FormationSlot + 4;
        FightToResult(session, bird);
        Ff7BattleSnapshot victory = battle.Capture();
        Check(battle is { Outcome: Ff7BattleOutcome.Victory, ResultReady: true } && victory.AiSaveBankDirty!.Contains((byte)0x54) && victory.AiSaveBank1![0x54] == 1, $"Original Chocobo AI failed to publish capture. Outcome={battle.Outcome}, actors={string.Join(',', battle.Actors.Select(actor => (actor.Actor.Slot, actor.Actor.HitPoints, actor.Actor.Conditions)))}.");
        using Ff7Session coldVictory = Ff7Session.Restore(installation, RoundTrip(session));
        Check(session.ConfirmBattleVictory() && coldVictory.ConfirmBattleVictory() && session.World is { PendingBattle: null } && session.World.ReadSpecial(5, -1) == 1, "Chocobo victory did not enter the original world battle return.");
        session.AdvanceTick(false);
        coldVictory.AdvanceTick(false);
        Check(session.World!.ControlledModel == 4 && session.World.Process.Chocobo?.RiderModel == 0 && session.World.Process.Audio?.Song == 2, "The original battle return script did not board the caught Chocobo.");
        Check(!session.ConfirmBattleVictory() && session.Progress.CompletedBattles.Count(receipt => receipt.RequestId == battle.RequestId) == 1 && Snapshot(session) == Snapshot(coldVictory), "Chocobo return replayed the capture or diverged after restoration.");
        Ff7WorldProcess mounted = session.World.Process;
        session.World.Execute(new(mounted.Vm.NextRequestId, -1, -1, 0, new(WorldOperation.Battle, [56])));
        Check(session.World.PendingBattle is { } next && next.Id > battle.RequestId, "World reentry reset the encounter identity and could alias an already settled battle.");
    }

    private static void TestSpecialReturns(Ff7Installation installation, Ff7SessionSave fieldSave, byte[] variables, Ff7WorldProcess ready, byte[] table)
    {
        ushort yuffieFormation = (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(2)) & 1023);
        Ff7SessionSave start = fieldSave with
        {
            Vm = fieldSave.Vm with { PersistentVariables = variables },
            World = ready,
            Progress = fieldSave.Progress with { Characters = fieldSave.Progress.Characters.Select(character =>
            {
                byte[] record = character.NativeRecord.ToArray();
                record[2] = 230;
                return character with { NativeRecord = record };
            }).ToArray() }
        };
        using Ff7Session special = Ff7Session.Restore(installation, start);
        special.SetFullCombatEnabled(true);
        special.SetRandomEncountersEnabled(false);
        long requestId = ready.Vm.NextRequestId;
        special.World!.Execute(new(requestId, -1, -1, 0, new(WorldOperation.Battle, [yuffieFormation | Ff7WorldBattleRequest.FieldReturnFlag])));
        using Ff7Session coldEntry = Ff7Session.Restore(installation, RoundTrip(special));
        special.AdvanceTick(false);
        coldEntry.AdvanceTick(false);
        Check(Snapshot(special) == Snapshot(coldEntry) && special.Battle!.Formation.FormationId == yuffieFormation, "Tagged world encounter lost its original formation or destination owner after restore.");
        special.AdvanceTick(false);
        coldEntry.AdvanceTick(false);
        Check(Snapshot(special) == Snapshot(coldEntry), "Tagged encounter changed the first logical battle callback after restore.");
        Ff7SessionSave active = RoundTrip(special);
        int[] specialFormations = Enumerable.Range(0, 8).SelectMany(band =>
        {
            int forest = BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(band * 4 + 2)) & 1023;
            return new[] { forest, forest + 1 };
        }).Distinct().ToArray();
        foreach (int formation in specialFormations)
        {
            using Ff7Session encounter = Ff7Session.Restore(installation, start);
            encounter.SetFullCombatEnabled(true);
            encounter.World!.Execute(new(requestId, -1, -1, 0, new(WorldOperation.Battle, [formation | Ff7WorldBattleRequest.FieldReturnFlag])));
            encounter.AdvanceTick(false);
            Check(encounter.Battle?.Formation.FormationId == formation, "Original special formation failed to initialize.");
            using Ff7Session restoredEncounter = Ff7Session.Restore(installation, RoundTrip(encounter));
            for (int callback = 0; callback < 150; callback++)
            {
                encounter.AdvanceTick(false, turnBasedBattle: true);
                restoredEncounter.AdvanceTick(false, turnBasedBattle: true);
            }
            Check(Snapshot(encounter) == Snapshot(restoredEncounter), "Original special formation diverged during cold turn continuation.");
        }

        using Ff7Session winning = Ff7Session.Restore(installation, active);
        FightToResult(winning);
        Check(winning.Battle is { Outcome: Ff7BattleOutcome.Victory, ResultReady: true }, $"Original Yuffie scene did not reach a playable victory: {winning.Battle!.Outcome}, {string.Join(", ", winning.Battle.Actors.Select(actor => (actor.Actor.Slot, actor.Actor.HitPoints, actor.Actor.Conditions)))}, ready={winning.Battle.ReadyPlayer}.");
        Ff7BattleSnapshot victory = winning.Battle!.Capture();
        string pending = Snapshot(winning);
        winning.ValidateWorldFieldDestination = _ => throw new IOException("fixture rejects destination");
        try
        {
            winning.ConfirmBattleVictory();
            throw new InvalidOperationException("A rejected special destination was adopted.");
        }
        catch (IOException)
        {
            Check(Snapshot(winning) == pending, "Failed special field preparation changed battle, world, or reward owners.");
        }
        winning.ValidateWorldFieldDestination = null;
        Check(winning.ConfirmBattleVictory() && winning.World is null && winning.Battle is null && winning.Field.Name == "yougan2", "Tagged world victory did not adopt the original field.");
        Check(winning.Host.Process.BattleResult == 17 && victory.AiSaveBank1?[2] == 17 && winning.Progress.CompletedBattles.Count(receipt => receipt.RequestId == victory.RequestId) == 1 && !winning.ConfirmBattleVictory(), $"Tagged world victory lost or duplicated the result. Field result={winning.Host.Process.BattleResult}, native byte={victory.AiSaveBank1?[2]}, receipts={winning.Progress.CompletedBattles.Count(receipt => receipt.RequestId == victory.RequestId)}.");
        using Ff7Session coldField = Ff7Session.Restore(installation, RoundTrip(winning));
        winning.AdvanceTick(false);
        coldField.AdvanceTick(false);
        Check(Snapshot(winning) == Snapshot(coldField), "Cold special field return replayed the encounter or changed field progression.");

        using Ff7Session escaping = Ff7Session.Restore(installation, active with { Battle = active.Battle! with { Outcome = Ff7BattleOutcome.Escaped, TerminalFrames = 8 } });
        for (int callback = 0; callback < 300 && !escaping.Battle!.ResultReady; callback++)
        {
            escaping.AdvanceTick(false);
        }
        Check(escaping.ConfirmBattleEscape() && escaping.World is { PendingBattle: null } && escaping.Progress.BattleEscapes == 1, "Tagged escape must return to World without a field departure.");
        Check(escaping.World!.Process.EntryModule == 1 && escaping.World.ReadSpecial(10, -1) == 1 && escaping.World.Process.Tick == 0, "Tagged escape did not preserve native battle-entry origin and escape bit.");
        using Ff7Session coldEscape = Ff7Session.Restore(installation, RoundTrip(escaping));
        escaping.AdvanceTick(false);
        coldEscape.AdvanceTick(false);
        Check(Snapshot(escaping) == Snapshot(coldEscape), "Tagged escape reentry changed the first world script callback after restore.");
        using Ff7Session losing = Ff7Session.Restore(installation, active with { Battle = active.Battle! with { Outcome = Ff7BattleOutcome.Defeat, TerminalFrames = 30 } });
        Check(losing.Battle is { ReturnsAfterDefeat: false } && !losing.ConfirmBattleDefeat(), "A tagged loss must retain the normal game-over boundary.");

        using Ff7WorldResources underwater = Ff7WorldResources.Load(installation, 2);
        IReadOnlyList<ushort> underwaterCode = underwater.ReadEvents().Code;
        Check(underwaterCode.Skip(0x2E0).Take(7).SequenceEqual(new ushort[] { 0x110, 0x4B3, 0x355, 0x100, 0x110, 0x3D8, 0x317 }), "Original underwater timed encounter changed its authored operands.");
        using Ff7Session timed = Ff7Session.Restore(installation, start);
        timed.SetFullCombatEnabled(true);
        timed.World!.Execute(new(requestId, -1, -1, 0, new(WorldOperation.Countdown, [1])));
        timed.World.Execute(new(requestId + 1, -1, -1, 1, new(WorldOperation.Battle, [0x3D8])));
        timed.AdvanceTick(false);
        Check(timed.Battle!.Capture().RequestFlags == 2 && timed.Clock.CountdownSeconds == 1, "World countdown was not transferred to the battle owner.");
        using Ff7Session coldTimed = Ff7Session.Restore(installation, RoundTrip(timed));
        timed.AdvanceClock(TimeSpan.FromSeconds(2));
        coldTimed.AdvanceClock(TimeSpan.FromSeconds(2));
        timed.AdvanceTick(false);
        coldTimed.AdvanceTick(false);
        Check(timed.Battle!.Outcome == Ff7BattleOutcome.Defeat && Snapshot(timed) == Snapshot(coldTimed), "Cold timed battle did not reach the same countdown-loss boundary.");
        Check(!timed.Battle.ReturnsAfterDefeat, "Native timer expiry follows the game-over branch, with no field or world result to publish.");

        Ff7SessionSave timedVictory = RoundTrip(timed);
        Ff7BattleSnapshot wonBeforeZero = timedVictory.Battle! with
        {
            Outcome = Ff7BattleOutcome.Victory,
            TerminalFrames = 49,
            Actors = timedVictory.Battle.Actors.Select(actor => actor.Actor.Slot >= 4
                ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | Ff7NativeBattleActionResolver.Death } } : actor).ToArray()
        };
        using Ff7Session timelyVictory = Ff7Session.Restore(installation, timedVictory with
        {
            Battle = wonBeforeZero,
            Progress = timedVictory.Progress with { Clock = new(CountdownSeconds: 1) }
        });
        for (int callback = 0; callback < 300 && !timelyVictory.Battle!.ResultReady; callback++)
        {
            timelyVictory.AdvanceTick(false);
        }
        Check(timelyVictory.ConfirmBattleVictory() && timelyVictory.World!.Process.EntryModule == 1 && timelyVictory.World.Process.Tick == 0, "Winning a timed world battle before zero did not prepare the original world module.");
        Check(timelyVictory.World!.ReadSpecial(10, -1) == 0, "Timed victory set the native escape bit.");

    }

    private static void FightToResult(Ff7Session session, int? excludedSlot = null)
    {
        Ff7BattleSession battle = session.Battle!;
        for (int tick = 0; tick < 6000 && !battle.ResultReady; tick++)
        {
            session.AdvanceTick(false, turnBasedBattle: true);
            if (battle.ReadyPlayer is int slot && battle.Outcome == Ff7BattleOutcome.Fighting)
            {
                Ff7BattleActorState? target = battle.Actors.FirstOrDefault(actor => actor.Actor.Slot >= 4 && actor.Actor.Slot != excludedSlot && actor.Actor.HitPoints > 0);
                if (target is not null)
                {
                    ushort targetMask = (ushort)(1 << target.Actor.Slot);
                    Ff7BattleCommandOption? attack = battle.Commands(slot).FirstOrDefault(option => option.Command is Ff7BattleCommand.Attack or Ff7BattleCommand.Limit && (option.ValidTargets & targetMask) != 0);
                    Check(attack is not null && battle.Submit(slot, attack.Command, attack.Argument, targetMask), "The native battle did not admit the expected attack command.");
                }
            }
        }
    }

    private static void TestSelection()
    {
        byte[] table = new byte[0x8A0];
        int regionTwoForest = 0xA0 + (2 * 4 + 2) * 32;
        table[regionTwoForest] = 1;
        table[regionTwoForest + 1] = 16;
        BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(regionTwoForest + 2), 0x8000 | 40);
        BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(regionTwoForest + 4), 0x7C00 | 41);
        Queue<byte> rolls = new([0, 255, 0, 0, 160]);
        int danger = 0;
        int formation = new Ff7WorldEncounterTable(table).Select(2 << 9 | 1, 1, false, 40, ref danger, rolls.Dequeue, out bool preemptive);
        Check(formation == 41 && preemptive && danger == 0 && rolls.Count == 0, "World selector lost native terrain priority, repeat reroll, or RNG ordering.");
        BinaryPrimitives.WriteUInt16LittleEndian(table, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(2), 268);
        BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(4), 25);
        BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(6), 270);
        rolls = new([0, 0]);
        danger = 0;
        formation = new Ff7WorldEncounterTable(table).Select(2 << 9 | 1, 1, true, 40, ref danger, rolls.Dequeue, out preemptive, cloudLevel: 21);
        Check(formation == (270 | Ff7WorldBattleRequest.FieldReturnFlag) && !preemptive && rolls.Count == 0 && danger == 1024, "Special selector changed native threshold comparison or consumed regular encounter RNG.");
        Ff7WorldEncounterRandom random = new(1234);
        for (int index = 0; index < 500; index++)
        {
            random.Next();
        }
        Ff7WorldEncounterRandom restored = new(random.Capture(), random.Index);
        for (int index = 0; index < 600; index++)
        {
            Check(random.Next() == restored.Next(), "World RNG restore changed a byte or refresh boundary.");
        }
    }

    private static string Snapshot(Ff7Session session) => JsonSerializer.Serialize(session.Capture());

    private static void TestGuestSettlement(Ff7SessionSave host, Ff7WorldBattleResultReceipt result)
    {
        string hash = new('0', 64);
        Ff7GuestBattleSettlementReceipt guest = new(Guid.NewGuid(), result.FieldGeneration, result.RequestId, result.Formation, 0, 0, hash, hash, 100, hash) { Outcome = result.Outcome };
        CombinedSessionSave combined = new() { ActiveGame = SessionGame.Ff7, Ff7 = host, Ff8Payload = [1], Ff8PayloadFormat = "fixture", GuestBattleSettlements = [guest] };
        using MemoryStream encoded = new();
        CombinedSessionCodec.Write(encoded, combined);
        encoded.Position = 0;
        CombinedSessionSave restored = CombinedSessionCodec.Read(encoded);
        Check(restored.Ff7!.Progress.WorldBattleResults.Single() == result, "Combined codec lost the completed world owner.");
        Reject(() => (combined with { GuestBattleSettlements = [guest with { RequestId = guest.RequestId + 1 }] }).Validate());
        Reject(() => (combined with { GuestBattleSettlements = [guest with { FormationId = (guest.FormationId + 1) % 1024 }] }).Validate());
        Reject(() => (combined with { GuestBattleSettlements = [guest with { Outcome = Ff7BattleOutcome.Defeat }] }).Validate());
        Reject(() => (combined with { Ff7 = host with { Progress = host.Progress with { WorldBattleResults = [] } } }).Validate());
        Reject(() => (host.Progress with { WorldBattleResults = [result, result] }).Validate());
        Reject(() => (host.Progress with { WorldBattleResults = [result with { Outcome = Ff7BattleOutcome.Fighting }] }).Validate());
        System.Text.Json.Nodes.JsonObject oldProgress = JsonSerializer.SerializeToNode(host.Progress)!.AsObject();
        oldProgress.Remove(nameof(Ff7Progress.WorldBattleResults));
        Ff7Progress compatible = oldProgress.Deserialize<Ff7Progress>()!;
        compatible.Validate();
        Check(compatible.WorldBattleResults.Length == 0 && compatible.BattleEscapes == host.Progress.BattleEscapes && compatible.NextEncounterRequestId == host.Progress.NextEncounterRequestId, "Absent world receipts changed old-save progress or encounter allocation.");
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("An invalid world battle result or guest settlement was accepted.");
    }

    private static Ff7SessionSave RoundTrip(Ff7Session session) => JsonSerializer.Deserialize<Ff7SessionSave>(Snapshot(session))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
