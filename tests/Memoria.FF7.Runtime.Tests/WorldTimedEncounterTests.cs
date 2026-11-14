using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldTimedEncounterTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed timed World encounter: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session field = Ff7Session.NewGame(installation);
        Ff7SessionSave fieldSave = field.Capture();
        CheckLiveRoute(installation, fieldSave, timed: true);
        CheckLiveRoute(installation, fieldSave, timed: false);
    }

    private static void CheckLiveRoute(Ff7Installation installation, Ff7SessionSave fieldSave, bool timed)
    {
        byte[] persistent = fieldSave.Vm.PersistentVariables.ToArray();
        persistent[0x350] |= 8;
        persistent[0x7A] |= 4;
        persistent[0x7A] &= 0x7F;
        persistent[0x7B] &= 0xEF;
        byte[] party = [0, 1, 2];
        party.CopyTo(persistent, 0x109);
        using Ff7WorldRuntime world = new(installation, persistent, 0x2A, party, worldId: 2);
        Ff7Progress progress = EquippedParty(fieldSave.Progress, underwater: !timed);
        using Ff7Session session = Ff7Session.Restore(installation, fieldSave with
        {
            Vm = fieldSave.Vm with { PersistentVariables = persistent },
            World = world.Process,
            Progress = progress
        });
        session.PublishNativeMenuPartyStatistics();
        for (int tick = 0; tick < 90; tick++)
        {
            session.AdvanceTick(false);
            if (session.World!.Process is { MainState: 1, FadeRate: 0, MovementEnabled: true })
            {
                break;
            }
        }
        Ff7WorldProcess ready = session.World!.Process;
        int approachSprite = Ff7PcExecutableData.LoadWorldEffects(installation).Sequences[7].FirstSprite;
        Check(ready is { WorldId: 2, ControlledModel: 13, MovementEnabled: true } && ready.Entities.Any(entity => entity.Model == 30), "The original WM2 startup must allocate and give control of the submarine with Emerald active.");
        using Ff7Session cold = Ff7Session.Restore(installation, Cold(session));
        int routeTicks = 0;
        bool sawBubble = false;
        bool sawFlash = false;
        for (; routeTicks < 250 && session.Battle is null; routeTicks++)
        {
            Ff7WorldProcess process = session.World.Process;
            Ff7WorldEntity submarine = process.Entities.Single(entity => entity.Model == 13);
            Ff7WorldEntity emerald = process.Entities.Single(entity => entity.Model == 30);
            int direction = (int)Math.Round(Math.Atan2(emerald.X - submarine.X, emerald.Z - submarine.Z) * 4096 / (2 * Math.PI));
            int turn = (2048 - direction - (process.Input?.CameraHeadingTarget ?? 0) + 2048 & 4095) - 2048;
            int horizontal = Math.Abs(turn) >= 16 ? Math.Sign(turn) : 0;
            int vertical = submarine.Y > emerald.Y + 30 ? -1 : 0;
            ushort held = Math.Abs(turn) <= 64 ? (ushort)0x20 : (ushort)0;
            session.SetInput(held, 0);
            cold.SetInput(held, 0);
            session.MovePlayer(horizontal, vertical, true);
            cold.MovePlayer(horizontal, vertical, true);
            session.AdvanceTick(false);
            cold.AdvanceTick(false);
            sawBubble |= session.World.Process.Effects?.DisplayParticles?.Any(particle => particle.Sprite == approachSprite) == true;
            sawFlash |= session.World.Process.WhiteFadeCallbacks > 0;
        }
        Ff7WorldBattleRequest? occurrence = session.World.PendingBattle;
        int formation = timed ? 984 : 986;
        Check(routeTicks < 250 && sawBubble && sawFlash && occurrence is { Scripted: true, ReturnsToWorldModule: true } &&
            occurrence.Timed == timed && occurrence.Formation == formation && occurrence.ScriptPc == (timed ? 0x2E6 : 0x2CF) &&
            session.Battle?.Formation.FormationId == formation && session.Battle.Capture().RequestFlags == (timed ? 2 : 0) &&
            session.Clock.CountdownSeconds == (timed ? 1203 : 0) && session.World.ReadPersistentByte(0x5F) == (timed ? 1 : 0) &&
            !session.CanSave && !session.CanSaveMemoria && Snapshot(session) == Snapshot(cold),
            $"Normal submarine input did not reach the shipped encounter {formation} in {routeTicks} callbacks. {occurrence}, bubble={sawBubble}, flash={sawFlash}, flags={session.Battle?.Capture().RequestFlags}, clock={session.Clock.CountdownSeconds}, cold={Snapshot(session) == Snapshot(cold)}.");
        if (timed)
        {
            session.AdvanceClock(TimeSpan.FromMinutes(21));
            session.AdvanceTick(false);
            Check(session.Battle is { Outcome: Ff7BattleOutcome.Defeat, ReturnsAfterDefeat: false } && !session.ConfirmBattleDefeat(), "Native Emerald Weapon timer expiry must reach Game Over.");
            return;
        }
        Ff7WorldEntity approach = session.World.Process.Entities.Single(entity => entity.Model == 13);
        WinBattle(installation, session);
        Check(session.Battle!.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 0, "The original Emerald actor was not defeated by player actions.");
        long battleId = session.Battle.RequestId;
        Ff7VictoryProjection preview = session.PreviewBattleVictory();
        using Ff7Session coldVictory = Ff7Session.Restore(installation, Cold(session));
        Check(session.ConfirmBattleVictory() && coldVictory.ConfirmBattleVictory() && !session.ConfirmBattleVictory(), "Live WM2 victory must settle exactly once.");
        Check(session.Progress.CompletedBattles.Count(receipt => receipt.RequestId == battleId) == 1 &&
            JsonSerializer.Serialize(session.Progress) == JsonSerializer.Serialize(preview.Progress), "WM2 victory must publish the original rewards exactly once.");
        for (int tick = 0; tick < 65; tick++)
        {
            session.AdvanceTick(false);
            coldVictory.AdvanceTick(false);
        }
        Ff7WorldProcess returned = session.World!.Process;
        Ff7WorldEntity parked = returned.Entities.Single(entity => entity.Model == 13);
        Check(returned is { WorldId: 2, EntryModule: 1, MainState: 1, ControlledModel: 13, MovementEnabled: true } &&
            parked.X == approach.PreviousX && parked.Z == approach.PreviousZ && returned.Entities.All(entity => entity.Model != 30) &&
            (session.World.ReadPersistentByte(0x7B) & 0x10) != 0 && session.World.ReadPersistentByte(0x5F) == 0 &&
            Snapshot(session) == Snapshot(coldVictory), "Victory must restore the original submarine pose, clear the timer, and suppress the defeated Emerald patrol.");
        session.SetInput(0x20, 0x20);
        session.AdvanceTick(false);
        Ff7WorldEntity moved = session.World.Process.Entities.Single(entity => entity.Model == 13);
        Check(moved.X != parked.X || moved.Z != parked.Z, "The returned submarine must accept thrust in WM2.");
    }

    private static Ff7Progress EquippedParty(Ff7Progress source, bool underwater)
    {
        byte[] party = [0, 1, 2];
        Ff7CharacterState[] characters = source.Characters.Select(character =>
        {
            if (!party.Contains((byte)character.Id))
            {
                return character;
            }
            byte[] record = character.NativeRecord.ToArray();
            record[1] = 99;
            Array.Fill(record, (byte)255, 2, 6);
            record[15] = 0;
            record[14] = 4;
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(34), 0);
            record[32] |= 1;
            record[29] = 29;
            record[30] = 255;
            Array.Fill(record, (byte)255, 64, 64);
            if (underwater && character.Id == 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 17);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), 9999);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(46), 9999);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(56), 9999);
            return character with { NativeRecord = record, Level = 99, HitPoints = 9999 };
        }).ToArray();
        return source with
        {
            Characters = characters,
            Party = [0, 1, 2],
            Inventory = [new(2, 99), new(5, 99), new(7, 99)],
            Clock = new(ElapsedSeconds: 24),
            NativeBattleSpeed = 0,
            FullCombatEnabled = true,
            RandomEncountersEnabled = false
        };
    }

    private static void WinBattle(Ff7Installation installation, Ff7Session session)
    {
        Ff7BattleSession battle = session.Battle!;
        Ff7BattleActorState emerald = battle.Actors.Single(actor => actor.Actor.Slot == 4);
        Check(emerald.Actor.HitPoints == 1_000_000 && emerald.Actor.MaxHitPoints == 1_000_000, "The original Emerald battle must start at its full HP.");
        int turns = 0;
        int callbacks = 0;
        TimeSpan battleStep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / Ff7BattleSession.LogicUpdatesPerSecond);
        bool verifiedLinkedContinuation = false;
        for (; callbacks < 240000 && !battle.ResultReady; callbacks++)
        {
            session.AdvanceClock(battleStep);
            session.AdvanceTick(advanceClock: false, turnBasedBattle: true);
            if (!verifiedLinkedContinuation && callbacks % 4 == 0 && battle.ReadyPlayer is null && !battle.NativeActorModelsReady && battle.Capture().ActorModels.Any(model => model.LinkedAction))
            {
                using Ff7Session restored = Ff7Session.Restore(installation, Cold(session));
                Check(Snapshot(session) == Snapshot(restored), "An active linked Emerald actor changed after cold restoration.");
                restored.AdvanceClock(battleStep);
                session.AdvanceClock(battleStep);
                restored.AdvanceTick(false, turnBasedBattle: true);
                session.AdvanceTick(false, turnBasedBattle: true);
                Check(Snapshot(session) == Snapshot(restored), "The next active linked Emerald callback changed after cold restoration.");
                verifiedLinkedContinuation = true;
            }
            if (battle.ReadyPlayer is not int slot || battle.Outcome != Ff7BattleOutcome.Fighting)
            {
                continue;
            }
            Ff7BattleActorState[] party = battle.Actors.Where(actor => actor.Actor.Slot < 3).ToArray();
            Ff7BattleActorState? knockedOut = party.FirstOrDefault(actor => actor.Actor.HitPoints == 0);
            Ff7BattleActorState injured = party.Where(actor => actor.Actor.HitPoints != 0).MinBy(actor => actor.Actor.HitPoints)!;
            IReadOnlyList<Ff7BattleCommandOption> commands = battle.Commands(slot);
            Ff7BattleCommandOption? selected = knockedOut is not null ? commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Item && option.Argument == 7) : null;
            int target = knockedOut?.Actor.Slot ?? slot;
            if (selected is null && injured.Actor.HitPoints < 3500)
            {
                selected = commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Item && option.Argument == 2)
                    ?? commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Item && option.Argument == 5);
                target = injured.Actor.Slot;
            }
            if (selected is null)
            {
                Ff7BattleActorState? enemy = battle.Actors.FirstOrDefault(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints != 0 && (actor.Actor.Conditions & 8) != 0);
                if (enemy is null)
                {
                    continue;
                }
                target = enemy.Actor.Slot;
                selected = commands.FirstOrDefault(option => option.Command == Ff7BattleCommand.Attack);
            }
            Check(selected is not null && battle.Submit(slot, selected.Command, selected.Argument, (ushort)(1 << target)), $"The combat input was rejected. Slot {slot}, target {target}, command {selected}.");
            turns++;
        }
        Check(verifiedLinkedContinuation && battle is { Outcome: Ff7BattleOutcome.Victory, ResultReady: true }, $"Emerald battle did not finish. Outcome={battle.Outcome}, turns={turns}, callbacks={callbacks}, actors={string.Join('/', battle.Actors.Select(actor => (actor.Actor.Slot, actor.Actor.HitPoints, actor.Actor.MaxHitPoints, actor.Actor.Conditions, actor.Actor.Statuses)))}, last={battle.Capture().LastHit}.");
    }

    private static string Snapshot(Ff7Session session) => JsonSerializer.Serialize(session.Capture());

    private static Ff7SessionSave Cold(Ff7Session session) => JsonSerializer.Deserialize<Ff7SessionSave>(Snapshot(session))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
