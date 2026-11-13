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
        Ff7WorldProcess resumed = returning.World!.Process;
        Check(JsonSerializer.Serialize(resumed.Entities) == JsonSerializer.Serialize(initial.Entities) && resumed.Tick == initial.Tick && resumed.Vm.System == initial.Vm.System with { Calls = resumed.Vm.System.Calls }, "World escape changed the retained actor placement or system PC.");
        using Ff7Session restored = Ff7Session.Restore(installation, RoundTrip(returning));
        Check(Snapshot(restored) == Snapshot(returning), "Cold restore after battle return replayed the world occurrence.");

        Ff7BattleSnapshot victory = during.Battle! with
        {
            Outcome = Ff7BattleOutcome.Victory,
            TerminalFrames = 45,
            Inventory = [new(0, 99)],
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

    private static Ff7SessionSave RoundTrip(Ff7Session session) => JsonSerializer.Deserialize<Ff7SessionSave>(Snapshot(session))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
