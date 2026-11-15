using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMinigameTests
{
    public static void RunReturnOnly()
    {
        TypedOpcode();
        StoryHealthConversion();
        InstalledStoryDeparture(returnOnly: true);
    }

    public static void Run()
    {
        TypedOpcode();
        StoryHealthConversion();
        InstalledStoryDeparture();
        InstalledLobbyDeparture();
    }

    private static void InstalledLobbyDeparture()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        FieldVariables variables = new(initial.Machine.Variables.CopyPersistent(), new byte[256]);
        variables.WriteWord(FieldBank.Persistent0Word, 0, 320);
        Ff7Progress progress = PrepareMotorballParty(initial.Progress) with { Party = [3, 1, 4] };
        for (int index = 0; index < progress.Party.Length; index++)
        {
            variables.WriteByte(FieldBank.Persistent1Byte, (byte)(9 + index), (byte)progress.Party[index]);
        }
        Ff7NativeSaveSlot slot = Ff7NativeSaveCodec.ExportField(progress, variables.CopyPersistent(), 264, -349, 16, 233, 0);
        using Ff7Session session = Ff7Session.Restore(installation, Ff7NativeSaveCodec.Import(slot, installation));
        Ff7AutoplayDriver driver = new(session);
        Ff7MidgarAutoPlayPolicy policy = new(session);
        Ff7MovieResources bike = Ff7MovieResources.Load(installation, 30);
        Check(File.Exists(bike.Path) && Path.GetFileName(bike.Path).Equals("bike.avi", StringComparison.OrdinalIgnoreCase) && bike.Cameras is { Count: > 0 }, "The native lobby bike movie and camera track must be installed.");
        const int lastBikeFrame = 750;
        int movieTicks = 0;
        for (int tick = 0; tick < 4000 && session.Host.PendingMinigame is null; tick++)
        {
            if (session.Host.Process.Movie is { PreparedId: 30, Playing: true })
            {
                int frame = movieTicks++ / 2;
                session.Host.PublishMovieFrame(frame, frame == lastBikeFrame);
            }
            foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => dialogue.AwaitingInput && !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
            {
                session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
            }
            try
            {
                if (policy.TryChooseInput(driver, out Ff7AutoplayInput input))
                {
                    driver.Apply(input);
                }
            }
            catch (NotSupportedException failure)
            {
                Ff7Actor? controlled = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
                throw new InvalidOperationException($"Lobby movement failed at {session.Field.Name}/{session.Machine.Tick}, actor {controlled?.Entity}, triangle {controlled?.Triangle}, position {controlled?.X4096 >> 12}/{controlled?.Y4096 >> 12}.", failure);
            }
            session.AdvanceTick();
        }
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Check(movieTicks == lastBikeFrame * 2 + 1, $"The installed lobby did not wait for its authored bike movie. Movie ticks {movieTicks}, field {session.Field.Name}/{session.Machine.Tick}, player triangle {player?.Triangle}, position {player?.X4096 >> 12}/{player?.Y4096 >> 12}, locked {session.Host.Process.PlayerLocked}, pending {string.Join(',', session.Machine.Capture().PendingRequests!.Select(request => $"{request.Entity}/{request.ProgramCounter:X4}"))}.");
        Check(session.Host.PendingMinigame is { Entity: 5, ProgramCounter: 0x06CB, Command: FieldMinigameCommand { Game: 0, Return.Map: 103 } }, $"The original lobby departure did not start G-bike at {session.Field.Name}/{session.Machine.Tick}, story {session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}, flags {session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x88):X2}/{session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0x5B):X2}, PCs {string.Join(',', session.Machine.Capture().Entities.Select(entity => entity.ProgramCounter.ToString("X")))}, dialogs {string.Join(',', session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed).Select(dialogue => $"{dialogue.Text}/{dialogue.AwaitingInput}"))}.");
        session.SetFullCombatEnabled(true);
        long beforeChase = session.FieldGeneration;
        for (int tick = 0; tick < 4200 && session.FieldGeneration == beforeChase; tick++)
        {
            session.SetHighwayInput(tick % 240 < 30 ? (ushort)0x2000 : (ushort)0, tick % 60 == 5 ? (ushort)0x20 : (ushort)0);
            session.AdvanceTick();
        }
        ushort truckGauge = unchecked((ushort)session.Machine.Variables.ReadWord(FieldBank.Persistent1Word, 0x74));
        int cloudHp = session.Progress.Characters.Single(character => character.Id == 0).HitPoints;
        for (int tick = 0; tick < 200 && session.Battle is null; tick++)
        {
            session.AdvanceTick();
        }
        Check(session.Field.Name == "blackbgb" && session.Highway is null && session.Host.Process.Menu is { Mode: 21, Completed: true } && session.Battle?.Capture().FormationId == 468, "The original lobby highway return must run its HP conversion and request Motorball.");
        Check(session.Progress.Characters.Single(character => character.Id == 0).HitPoints == Math.Max(1, cloudHp * truckGauge / ushort.MaxValue), "The native lobby return must apply the saved highway gauge once before Motorball.");
        RunMotorball(installation, session);
        Console.WriteLine("PASS original lobby blackbgb Start prompt, highway, HP conversion, Motorball victory and world exit");
    }

    private static void TypedOpcode()
    {
        byte[] code = [0, 0x20, 0x65, 0, 0x2E, 0xFB, 0xDE, 0xFF, 0x32, 0, 0x81, 0xFE, 0x80, 0x50, 9, 87, 0x12, 0];
        FieldProgram program = new(code, [new ushort[32]]);
        RecordingHost host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.AdvanceTick();
        FieldHostRequest request = machine.Capture().PendingRequests!.Single();
        Check(request.ProgramCounter == 1 && request.Command == new FieldMinigameCommand(new(101, -1234, -34, 50, 0x81), 0xFE), "MINIGAME changed the raw destination packet.");
        RecordingHost coldHost = new();
        FieldMachine cold = FieldMachine.Restore(program, RoundTrip(machine.Capture()), coldHost);
        cold.AdvanceTick();
        Check(coldHost.Request == request && cold.Capture().Entities[0].ProgramCounter == 1 && cold.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "A waiting MINIGAME must retain its PC and request across cold restore.");
        coldHost.Complete = true;
        cold.AdvanceTick();
        Check(cold.Capture().PendingRequests!.Length == 0 && cold.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "MINIGAME completion must advance 11 bytes and continue in the same pass.");
        Console.WriteLine("PASS MINIGAME raw operands, pending owner, cold continuation and completion length");
    }

    private static void StoryHealthConversion()
    {
        FieldVariables variables = new();
        int[] hp = [0, 9999, 127, 500, 9999, 42];
        ushort[] gauges = [65535, 65535, 32768, 0, 10000];
        int[] expected = [1, 9999, 63, 1, 1525, 42];
        for (int index = 0; index < gauges.Length; index++)
        {
            variables.WriteWord(FieldBank.Persistent1Word, (byte)(0x74 + index * 2), unchecked((short)gauges[index]));
        }
        for (int index = 0; index < hp.Length; index++)
        {
            byte[] record = new byte[132];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), (ushort)hp[index]);
            Ff7CharacterState source = new(index, string.Empty, 1, 0, hp[index], 0, record);
            Ff7CharacterState result = Ff7Session.ApplyHighwayHealth(source, variables);
            Check(result.HitPoints == expected[index] && System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(result.NativeRecord.AsSpan(44)) == expected[index], $"Highway menu mode 21 changed the original HP scaling for slot {index}.");
        }
        Console.WriteLine("PASS highway native menu HP conversion and unaffected character slots");
    }

    private static void InstalledStoryDeparture(bool returnOnly = false)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original MINIGAME handoff. Configure MEMORIA_FF7_GAME_DIR.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(directory);
        PcHighwayCourse storyCourse = Ff7HighwayResources.Load(installation, arcade: false);
        PcHighwayCourse arcadeCourse = Ff7HighwayResources.Load(installation, arcade: true);
        Check(storyCourse.Track.Count == 67 && storyCourse.Track.Sum(command => command.Length) == 4950 && storyCourse.Events.Count == 17 && storyCourse.Events[^1] == new PcHighwayEvent(1795, 20), "The installed highway story course lost its native track commands or finish event.");
        Check(arcadeCourse.Track.Count == 65 && arcadeCourse.Events.Count == 16 && arcadeCourse.Events[^1] == new PcHighwayEvent(1767, 21), "The installed highway arcade course lost its distinct track or event table.");

        Check(storyCourse.Paths.Count == 10 && storyCourse.Paths[2].PointCount == 443 && storyCourse.Paths[2].Sample(0x8000, true) == new Vector3(767, 376, 8218) && storyCourse.Paths[8].Sample(0x8000, true) == new Vector3(8, 0, -1) && storyCourse.Paths[8].Sample(0x8000, false) == new Vector3(8, 0, 1), "The installed highway paths lost their signed fixed-point interpolation.");

        using Ff7Session initial = Ff7Session.NewGame(installation);
        FieldVariables variables = new(initial.Machine.Variables.CopyPersistent(), new byte[256]);
        variables.WriteWord(FieldBank.Persistent0Word, 0, 326);
        Ff7Progress progress = PrepareMotorballParty(initial.Progress);
        Ff7NativeSaveSlot slot = Ff7NativeSaveCodec.ExportField(progress, variables.CopyPersistent(), 101, 0, 0, 3, 0);
        Ff7SessionSave entry = Ff7Session.ImportNativeField(slot, installation);
        using Ff7Session session = Ff7Session.Restore(installation, entry);
        session.AdvanceTick();

        Ff7SessionSave pending = session.Capture();
        FieldHostRequest request = pending.Vm.PendingRequests!.Single(request => request.Command is FieldMinigameCommand);
        Check(request.Entity == 0 && request.ProgramCounter == 0x0143 && request.Command == new FieldMinigameCommand(new(101, 0, 0, 3, 0), 0), "The original director did not produce its native story highway request.");
        Check(session.Field.Name == "blackbg9" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 332 && session.Host.PendingMinigame == request && !session.CanSave && !session.CanSaveMemoria && session.Battle is null, "The original field must yield to the highway owner after writing its story moment.");
        using Ff7Session cold = Ff7Session.Restore(installation, RoundTrip(pending));
        session.AdvanceTick();
        cold.AdvanceTick();
        Ff7HighwayStart prepared = cold.HighwayStart!;
        Check(prepared.Request == request && !prepared.Arcade && prepared.HighScore == 0 && prepared.InitialSpeed == 122 && prepared.InitialTrackSegment == 80 && prepared.FirstDueEvent == new PcHighwayEvent(1, 10), "Highway preparation lost its original owner, story mode, race registers or first event.");
        Check(prepared.Road[0] == new Ff7HighwayRoadSegment(256, 700, 0, 0, false) && prepared.Road[7] == new Ff7HighwayRoadSegment(2048, 700, 1, 7, false) && prepared.Road[8] == new Ff7HighwayRoadSegment(2304, 700, 3, 0, false) && prepared.Road[^1] == new Ff7HighwayRoadSegment(20480, 700, 1, 7, false), "The highway road ring changed from native initialization.");
        string surfaceHash = Convert.ToHexString(SHA256.HashData(prepared.Road.Select(segment => segment.Surface).ToArray()));
        Check(surfaceHash == "0135059F251685B15D5EB16D8B55DCC56C17CF2381CA1EFBF18BE9D786431AA2", "The original highway startup surface sequence changed.");
        Check(prepared.Gauges.Count == 5 && prepared.Gauges.All(gauge => gauge == new Ff7HighwayGauge(65535, 65535)), "The initial highway gauges lost their unsigned maximum.");
        cold.AdvanceTick();
        Check(ReferenceEquals(prepared, cold.HighwayStart) && session.HighwayStart is { } original && original.Request == prepared.Request && original.Road.SequenceEqual(prepared.Road), "A repeated or cold highway start changed the prepared startup.");
        Check(cold.Highway is { Tick: 1, TrackPosition: 0x237A, OverlayFlags: 1 } && cold.Machine.Tick == pending.Vm.Tick, "The first chase update lost the native first event or advanced field scripts.");
        PlayHighwayToReturn(installation, session, cold, pending, returnOnly);
        if (returnOnly)
        {
            Console.WriteLine("PASS original highway return, cold departure and native gauge publication");
            return;
        }
        RejectCorruptMinigameOwner(installation, pending, request);

        FieldVariables arcadeVariables = new(entry.Vm.PersistentVariables, entry.Vm.MapVariables);
        arcadeVariables.WriteByte(FieldBank.Persistent1Byte, 0x73, 1);
        arcadeVariables.WriteWord(FieldBank.Persistent2Word, 0x6D, unchecked((short)0xFEDC));
        using Ff7Session arcade = Ff7Session.Restore(installation, entry with { Vm = entry.Vm with { PersistentVariables = arcadeVariables.CopyPersistent() } });
        arcade.AdvanceTick();
        Ff7SessionSave arcadePending = arcade.Capture();
        FieldHostRequest arcadeRequest = arcadePending.Vm.PendingRequests!.Single(value => value.Command is FieldMinigameCommand);
        RequireHighwayBoundary(arcade, arcadePending, arcadeRequest);
        Check(arcade.HighwayStart is { Arcade: true, HighScore: 0xFEDC, FirstDueEvent: { Segment: 1, Code: 12 } }, "The original highway mode and unsigned high score did not select the arcade course.");
        Console.WriteLine("PASS installed story chase, Motorball victory, Reform and physical exit to world entry 1");
    }

    private static void PlayHighwayToReturn(Ff7Installation installation, Ff7Session session, Ff7Session cold, Ff7SessionSave pending, bool returnOnly = false)
    {
        Ff7HighwayRuntime race = session.Highway ?? throw new InvalidOperationException("The original story request did not initialize its chase.");
        Ff7HighwayRuntime restored = cold.Highway!;
        session.SetFullCombatEnabled(true);
        cold.SetFullCombatEnabled(true);
        session.AdvanceTick();
        bool checkedCurve = false;
        bool checkedFade = false;
        for (int frame = 1; frame < 4147; frame++)
        {
            ushort held = frame % 240 < 30 ? (ushort)0x2000 : frame % 240 is >= 150 and < 170 ? (ushort)0x8000 : (ushort)0;
            ushort pressed = frame % 60 == 5 ? (ushort)0x20 : frame % 60 == 35 ? (ushort)0x80 : (ushort)0;
            session.SetHighwayInput(held, pressed);
            cold.SetHighwayInput(held, pressed);
            session.AdvanceTick();
            cold.AdvanceTick();
            Check(race.Player == restored.Player && race.Gauges.SequenceEqual(restored.Gauges), "The original pending request did not reconstruct the same chase.");
            if (race.Tick is 4011 or 4012 or 4013)
            {
                Check(race.FadingOut == (race.Tick >= 4012) && race.FadeAlpha == Math.Max(0, (race.Tick - 4011) * 5), "The story highway visible fade changed its native start or alpha step.");
                checkedFade = true;
            }
            if (!checkedCurve && race.TrackSegment == 101)
            {
                Ff7HighwayRoadSegment curve = race.Road[^1];
                Check((curve.X, curve.Y, curve.Z, curve.Yaw, curve.Roll) == (3, 0, 25855, 10, 5), "The first curved road segment changed from the original callback.");
                checkedCurve = true;
            }
            if (frame == 1)
            {
                Check(race.Player.X > 0 && race.Tick == 2 && session.Machine.Tick == pending.Vm.Tick, "Highway input failed to steer Cloud or advanced the suspended field.");
            }
        }
        Check(checkedCurve && checkedFade && race.Tick == 4147 && race.TrackSegment == 1937 && race.TrackPosition == 475414 + 0x2300 && race.FadeAlpha == 255 && race.Finished, "The story highway timing changed from the original warmed-up course.");
        Check(race.Gauges.Any(gauge => gauge.Value < gauge.Maximum), "The chase produced no enemy or vehicle damage.");
        Check(session.Highway is null && session.MapId == 101 && session.FieldGeneration == pending.FieldGeneration + 1 && session.Machine.Tick == 0 && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 332, "The original minigame return did not enter a fresh blackbg9.");
        Check(session.Progress.Characters.Select(character => character.HitPoints).SequenceEqual(pending.Progress.Characters.Select(character => character.HitPoints)), "The native retirement applied field HP before an authored consumer.");
        ushort[] written = Enumerable.Range(0, 5).Select(index => unchecked((ushort)session.Machine.Variables.ReadWord(FieldBank.Persistent1Word, (byte)(0x74 + index * 2)))).ToArray();
        Check(written.SequenceEqual(new[] { 0, 1, 3, 2, 4 }.Select(index => race.Gauges[index].Value)), "The native return did not publish the five gauges in character order.");
        if (returnOnly)
        {
            return;
        }
        session.AdvanceTick();
        Check(session.Battle?.Capture().FormationId == 468, "The original blackbg9 director did not start Motorball after the chase.");
        RunMotorball(installation, session);
    }

    private static Ff7Progress PrepareMotorballParty(Ff7Progress progress)
    {
        Ff7CharacterState[] characters = progress.Characters.Select(character =>
        {
            if (character.Id > 4)
            {
                return character;
            }
            byte[] record = character.NativeRecord.ToArray();
            foreach (int offset in new[] { 44, 46, 56 })
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(offset), 1200);
            }
            return character with { HitPoints = 1200, NativeRecord = record };
        }).ToArray();
        return progress with { Characters = characters, Party = [0, 1, 2], Inventory = [new(0, 15), new(1, 10), new(7, 10)] };
    }

    private static void RunMotorball(Ff7Installation installation, Ff7Session session)
    {
        Ff7BattleSession battle = session.Battle!;
        bool sawClockPause = false;
        bool sawDelayedEffect = false;
        bool checkedCold = false;
        for (int tick = 0; tick < 65000 && !battle.ResultReady; tick++)
        {
            if (!checkedCold && battle.Action?.DelayedEffectFrames is > 0)
            {
                Ff7SessionSave snapshot = session.Capture();
                using Ff7Session restored = Ff7Session.Restore(installation, RoundTrip(snapshot));
                session.AdvanceTick();
                restored.AdvanceTick();
                Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(restored.Capture()), "The pending Motorball effect diverged across cold restore.");
                checkedCold = true;
                sawDelayedEffect = true;
                continue;
            }
            if (battle.ReadyPlayer is int slot)
            {
                IReadOnlyList<Ff7BattleCommandOption> options = battle.Commands(slot);
                Ff7BattleActorState? fallen = battle.Actors.FirstOrDefault(actor => actor.Actor.Slot < 3 && actor.Actor.HitPoints == 0);
                Ff7BattleActorState? injured = battle.Actors.Where(actor => actor.Actor.Slot < 3 && actor.Actor.HitPoints > 0 && actor.Actor.HitPoints * 2 <= actor.Actor.MaxHitPoints).OrderBy(actor => actor.Actor.HitPoints).FirstOrDefault();
                Ff7BattleCommandOption? revive = options.FirstOrDefault(option => option.Command == Ff7BattleCommand.Item && option.Argument == 7 && option.UnsupportedReason is null && fallen is not null && (option.ValidTargets & 1 << fallen.Actor.Slot) != 0);
                Ff7BattleCommandOption? heal = options.FirstOrDefault(option => option.Command == Ff7BattleCommand.Item && option.Argument == 1 && option.UnsupportedReason is null && injured is not null && (option.ValidTargets & 1 << injured.Actor.Slot) != 0);
                Ff7BattleCommandOption? attack = options.FirstOrDefault(option => option.Command is Ff7BattleCommand.Attack or Ff7BattleCommand.Limit && option.UnsupportedReason is null && (option.ValidTargets & 0x3F0) != 0);
                Ff7BattleCommandOption? command = revive ?? heal ?? attack;
                if (command is not null)
                {
                    ushort targets = revive is not null ? (ushort)(1 << fallen!.Actor.Slot) : heal is not null ? (ushort)(1 << injured!.Actor.Slot) : (ushort)(command.ValidTargets & 0x3F0);
                    targets = (ushort)(targets & -targets);
                    Check(battle.Submit(slot, command.Command, command.Argument, targets), $"Motorball rejected {command.Command}/{command.Argument} from {slot} targeting {targets:X} at {battle.Tick}.");
                }
            }
            try
            {
                sawClockPause |= battle.Capture().ActionClockPaused;
                session.AdvanceTick(advanceClock: true, confirmBattleLimit: battle.BeatRushSelecting);
            }
            catch (NotSupportedException failure)
            {
                throw new InvalidOperationException($"Motorball layout {battle.Layout} callback {battle.Tick}, action {battle.Action?.Command}, animation {battle.Action?.Animation}.", failure);
            }
        }
        Check(sawClockPause && sawDelayedEffect && battle.ResultReady && battle.Outcome == Ff7BattleOutcome.Victory && session.ConfirmBattleVictory(), $"Motorball did not complete with native victory: {battle.Outcome} at callback {battle.Tick}, action {battle.Action?.Command.AttackId}, HP {string.Join(',', battle.Actors.Select(actor => actor.Actor.HitPoints))}.");
        for (int tick = 0; tick < 900 && session.Field.Name is "blackbg9" or "blackbgb"; tick++)
        {
            session.AdvanceTick();
        }
        Check(session.Field.Name == "roadend", "The original settled Motorball director did not enter the road-end scene.");
        Ff7AutoplayDriver driver = new(session);
        Ff7MidgarAutoPlayPolicy policy = new(session);
        bool checkedPartyMenu = false;
        for (int tick = 0; tick < 8000 && session.World is null; tick++)
        {
            if (session.Host.Process.Menu is { Mode: 7, Completed: false } menu)
            {
                Check(!session.CompleteStoryPartyMenu(menu.RequestId + 1, session.Progress.Party) && !session.CompleteStoryPartyMenu(menu.RequestId, [0, 0, 1]), "The scripted party menu accepted invalid owner or party selection.");
                int[] members = session.Progress.Party.Where(id => id != 255 && (session.Progress.AvailableCharacters & 1 << id) != 0).Concat(Enumerable.Range(0, 9).Where(id => (session.Progress.AvailableCharacters & 1 << id) != 0)).Distinct().Take(3).ToArray();
                Check(session.CompleteStoryPartyMenu(menu.RequestId, members), "The departure party selection could not complete.");
                checkedPartyMenu = true;
            }
            foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => dialogue.AwaitingInput && !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
            {
                Ff7Dialogue selected = policy.ChooseDialogueSelection(dialogue) is int choice ? Ff7DialogueCursor.Select(dialogue, choice) : dialogue;
                session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(selected, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
            }
            try
            {
                if (policy.TryChooseInput(driver, out Ff7AutoplayInput input))
                {
                    driver.Apply(input);
                }
            }
            catch (NotSupportedException failure)
            {
                Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                throw new InvalidOperationException($"Road end navigation failed at {session.Field.Name}/{session.Machine.Tick} actor {player.Entity}, triangle {player.Triangle}, position ({player.X4096 >> 12},{player.Y4096 >> 12},{player.Z4096 >> 12}), story {session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}.", failure);
            }
            session.AdvanceTick();
        }
        Ff7Actor? exiting = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Check(checkedPartyMenu && session.World is { Process.WorldId: 0, Process.EntrySelector: 1 }, $"The road-end route remained at {session.Field.Name}/{session.Machine.Tick}, story {session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}, locked {session.Host.Process.PlayerLocked}, mapjump disabled {session.Host.Process.MapJumpDisabled}, triangle {exiting?.Triangle}, position ({exiting?.X4096 >> 12},{exiting?.Y4096 >> 12},{exiting?.Z4096 >> 12}), PCs {string.Join(',', session.Machine.Capture().Entities.Take(4).Select(entity => entity.ProgramCounter.ToString("X")))}.");
        Ff7SessionSave world = RoundTrip(session.Capture());
        using Ff7Session coldWorld = Ff7Session.Restore(installation, world);
        Check(coldWorld.World is { Process.WorldId: 0, Process.EntrySelector: 1 }, "The physical Midgar exit was lost on cold restore.");
    }

    private static void RequireHighwayBoundary(Ff7Session session, Ff7SessionSave pending, FieldHostRequest request)
    {
        try
        {
            session.AdvanceTick();
            throw new InvalidOperationException("An unsupported highway module ran or returned to the field.");
        }
        catch (FieldExecutionException error) when (error.InnerException is NotSupportedException)
        {
            Check(error.InnerException.Message.Contains("arcade highway", StringComparison.Ordinal) && error.Context.Opcode == 0x20 && error.Context.Entity == request.Entity && error.Context.ProgramCounter == request.ProgramCounter, "The highway boundary lost its native opcode owner.");
            Ff7SessionSave after = session.Capture();
            Check(after.Vm.Tick == pending.Vm.Tick && after.FieldGeneration == pending.FieldGeneration && after.Vm.PersistentVariables.SequenceEqual(pending.Vm.PersistentVariables) && session.Host.PendingMinigame == request, "The refused minigame consumed field progress or returned a synthetic result.");
        }
    }

    private static void RejectCorruptMinigameOwner(Ff7Installation installation, Ff7SessionSave pending, FieldHostRequest request)
    {
        try
        {
            (pending with { Vm = pending.Vm with { PendingRequests = [] } }).Validate();
            throw new InvalidOperationException("The snapshot accepted a minigame without its VM request.");
        }
        catch (InvalidDataException)
        {
        }

        FieldMinigameCommand command = (FieldMinigameCommand)request.Command;
        FieldHostRequest changed = request with { Command = command with { Game = 2 } };
        Ff7SessionSave mismatched = pending with
        {
            Vm = pending.Vm with { PendingRequests = [changed] },
            Process = pending.Process with { Receipts = pending.Process.Receipts.Select(receipt => receipt.Request == request ? receipt with { Request = changed } : receipt).ToArray() }
        };
        try
        {
            using Ff7Session _ = Ff7Session.Restore(installation, mismatched);
            throw new InvalidOperationException("The snapshot accepted a minigame type changed from its native instruction.");
        }
        catch (InvalidDataException)
        {
        }
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingHost : IFieldHost
    {
        public FieldHostRequest? Request { get; private set; }
        public bool Complete { get; set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Request = request;
            return new(Complete);
        }
    }
}
