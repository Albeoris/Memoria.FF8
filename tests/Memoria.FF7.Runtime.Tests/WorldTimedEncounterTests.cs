using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
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
        CheckEmeraldEncounter(installation, fieldSave, timed: true);
        CheckEmeraldEncounter(installation, fieldSave, timed: false);
    }

    private static void CheckEmeraldEncounter(Ff7Installation installation, Ff7SessionSave fieldSave, bool timed)
    {
        byte[] variables = fieldSave.Vm.PersistentVariables.ToArray();
        variables[0x350] |= 1 << 3;
        variables[0x5F] = timed ? (byte)2 : (byte)0;
        if (!timed)
        {
            variables[0x7A] |= 1 << 7;
        }
        using Ff7WorldRuntime fixture = new(installation, variables, 0x2A, variables.AsSpan(0x109, 3).ToArray(), worldId: 2);
        PcWorldEvents events = fixture.Resources.ReadEvents();
        Check(events.Functions.Any(function => function.Header == 0x5E02 && function.Offset == 0x2A9), "The original Emerald Weapon model must own the underwater encounter event.");
        int startPc = timed ? 0x2DF : 0x2CC;
        ushort formation = timed ? (ushort)984 : (ushort)986;
        ushort[] instructions = timed ? [0x100, 0x110, 1203, 0x355, 0x100, 0x110, formation, 0x317] : [0x100, 0x110, formation, 0x317];
        Check(events.Code.Skip(startPc).Take(instructions.Length).SequenceEqual(instructions), "The bounded original WM2 continuation must execute the shipped battle instructions.");

        Ff7WorldProcess start = fixture.Process;
        WorldEntitySnapshot emerald = new(30, startPc, 0, 0, false, 13, []);
        start = start with
        {
            MainState = 1,
            Initialized = true,
            Audio = new(Initialized: true, Song: 1, PlaybackEnabled: true),
            ControlledModel = 13,
            MovementEnabled = false,
            Entities = [new(13, X: 137907, Y: -3000, Z: 136257, Direction: 0x7B0, TargetDirection: 0x7B0, Flags: 128), new(30, X: 137907, Y: -4250, Z: 136257, Flags: 128)],
            Vm = start.Vm with { Tick = 1, System = start.Vm.System with { ProgramCounter = 0 }, Models = [emerald, new(13, 0, 0, 0, false, 0, [])] }
        };
        using Ff7Session direct = Ff7Session.Restore(installation, fieldSave with { Vm = fieldSave.Vm with { PersistentVariables = variables }, World = start });
        direct.SetFullCombatEnabled(true);
        direct.SetRandomEncountersEnabled(false);
        using Ff7Session coldSource = Ff7Session.Restore(installation, Cold(direct));
        direct.AdvanceTick(false);
        coldSource.AdvanceTick(false);
        Check(direct.World!.Process is { WorldId: 2, Vm.Unloading: true, BattleState.Pending: { Scripted: true, ReturnsToWorldModule: true } occurrence } &&
            occurrence.ScriptPc == startPc + instructions.Length - 1 && occurrence.Formation == formation && occurrence.Timed == timed, "The shipped WM2 model script did not publish its occurrence and complete unload.");
        Check(direct.Battle?.Formation.FormationId == formation && direct.Battle.Capture().RequestFlags == (timed ? 2 : 0) &&
            direct.Clock.CountdownSeconds == (timed ? 1203 : 0) && direct.World.ReadPersistentByte(0x5F) == (timed ? 1 : 0) &&
            !direct.CanSave && !direct.CanSaveMemoria, "The encounter did not transfer its authored countdown, formation and save boundary.");
        Check(Snapshot(direct) == Snapshot(coldSource), "Cold WM2 model execution published a different encounter.");

        Ff7SessionSave battle = Cold(direct);
        using Ff7Session coldBattle = Ff7Session.Restore(installation, battle);
        direct.AdvanceTick(false, turnBasedBattle: true);
        coldBattle.AdvanceTick(false, turnBasedBattle: true);
        Check(Snapshot(direct) == Snapshot(coldBattle), "Cold Emerald Weapon initialization changed its first battle callback.");

        if (timed)
        {
            using Ff7Session expiry = Ff7Session.Restore(installation, battle);
            expiry.AdvanceClock(TimeSpan.FromMinutes(21));
            expiry.AdvanceTick(false);
            Check(expiry.Battle is { Outcome: Ff7BattleOutcome.Defeat, ReturnsAfterDefeat: false } && !expiry.ConfirmBattleDefeat(), "Native Emerald Weapon timer expiry must reach Game Over.");
        }

        Ff7BattleSnapshot victory = CaptureEmeraldVictory(installation, battle.Battle!, direct.Battle!.Data);
        using Ff7Session winning = Ff7Session.Restore(installation, battle with { Battle = victory });
        Ff7WorldRuntime source = winning.World!;
        Check(winning.ConfirmBattleVictory() && !winning.ConfirmBattleVictory(), "WM2 must settle victory exactly once.");
        Check(winning.World is { PendingBattle: null } replacement && replacement != source && replacement.Process is { WorldId: 2, EntryModule: 1, Tick: 0, Initialized: false, Vm.Unloading: false } &&
            replacement.ReadSpecial(10, -1) == 0 && (replacement.ReadPersistentByte(0x7B) & 0x10) != 0, "Victory must reenter the original underwater map and publish the native defeat flag.");
        using Ff7Session coldReturn = Ff7Session.Restore(installation, Cold(winning));
        Check(Snapshot(winning) == Snapshot(coldReturn), "Cold restoration changed the WM2 reentry boundary.");
        winning.AdvanceTick(false);
        coldReturn.AdvanceTick(false);
        Ff7WorldProcess resumed = winning.World!.Process;
        Ff7WorldEntity submarine = resumed.Entities.Single(entity => entity.Model == 13);
        Check(resumed is { WorldId: 2, EntryModule: 1, Tick: 1, Initialized: true, ControlledModel: 13, ViewMode: 2, SubmarineTint: 6 } &&
            submarine is { X: 137907, Y: -3000, Z: 136257, TargetDirection: 0x7B0 } && winning.World.ReadPersistentByte(0x5F) == 0 &&
            Snapshot(winning) == Snapshot(coldReturn), "The first original WM2 reentry callback did not restore the submerged vehicle or changed after a cold load.");
    }

    private static Ff7BattleSnapshot CaptureEmeraldVictory(Ff7Installation installation, Ff7BattleSnapshot battle, SceneBattleData data)
    {
        SceneBattleEnemy emerald = data.Enemies.Single(enemy => enemy.Placement.EnemyId == 13);
        int slot = emerald.FormationSlot + 4;
        Ff7BattleActorState[] defeated = battle.Actors.Select(actor => actor.Actor.Slot >= 4
            ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | Ff7NativeBattleActionResolver.Death } }
            : actor).ToArray();
        Ff7BattleAiActor[] actors = defeated.Select(actor => actor.Actor).ToArray();
        Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(actors, 0);
        Ff7BattleAiContext context = new(slot, actors, masks.Targetable, masks.KnockedOut, masks.ExcludedOpponents, 3, GlobalMemory: battle.AiGlobalMemory, SaveBank1: battle.AiSaveBank1);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7BattleRng random = new(kernel[2].Data.Span, battle.Random);
        Ff7BattleAiResult death = Ff7BattleAi.RunActorCallback(context, defeated.Single(actor => actor.Actor.Slot == slot).AiMemory, data, random, kernel[1].Data);
        Check(death.SaveBank1Writes.TryGetValue(0x7B, out byte story) && (story & 0x10) != 0, "The original Emerald Weapon death callback must set its defeat flag.");
        byte[] saveBank = battle.AiSaveBank1!.ToArray();
        saveBank[0x7B] = story;
        return battle with { Outcome = Ff7BattleOutcome.Victory, TerminalFrames = 49, Actors = defeated, AiSaveBank1 = saveBank, AiSaveBankDirty = [0x7B] };
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
