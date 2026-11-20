using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeNavigationPreparationTests
{
    public static void Run()
    {
        CheckFadePrefix();
        byte[] script = [0x24, 30, 0, 0x6D, 6, 0, 0, 0];
        FieldScriptAnalysis analysis = new(true, null, "navigation-prelude", false, [], [0, 3, 7], [])
        {
            MayChangeNavigation = true,
            UnblockedTriangles = [6],
            InstructionSites = [new(0, 0), new(0, 3), new(0, 7)]
        };
        int[] saved = new int[8];
        saved[7] = 7;
        FieldEntitySnapshot owner = new(0, 6, 30, saved, new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "navigation-preparation", 1, true, new byte[1280], new byte[256], [owner]);
        Ff7FieldProcess process = new() { BlockedTriangles = [0x40] };
        string before = JsonSerializer.Serialize(new { vm, process });
        Ff7AutoPlayNativeReadiness readiness = new();
        Check(readiness.ShouldWaitForNavigationPreparation(1, "source", vm, process, script, (_, _) => analysis), "An admitted autonomous navigation prelude was not scheduled.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWaitForNavigationPreparation(1, "source", vm with { Entities = [owner with { Priority = 7 }] }, process, script, (_, _) => analysis), "Ambient script priority authenticated a navigation prelude.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWaitForNavigationPreparation(1, "source", vm with { Entities = [owner with { SavedProgramCounters = new int[8] }] }, process, script, (_, _) => analysis), "An unadmitted script authenticated a prelude.");
        Check(!readiness.ShouldWaitForNavigationPreparation(1, "source", vm, process with { BlockedTriangles = [0] }, script, (_, _) => analysis), "An already-open triangle kept speculative preparation alive.");
        foreach (FieldScriptAnalysis unknown in new[]
        {
            analysis with { Complete = false },
            analysis with { Reads = [new(FieldBank.MapByte, 1, false)] },
            analysis with { HostReads = [new(FieldHostReadKind.Random)] },
            analysis with { UnblockedTriangles = [] }
        })
        {
            Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(unknown, 1, [0x40], script), "Unknown, variable-dependent or irrelevant work authenticated preparation.");
        }
        foreach (byte opcode in new byte[] { 0x10, 0x30, 0x31, 0x32, 0x40, 0xF9, 0xFF })
        {
            byte[] unsafeScript = (byte[])script.Clone();
            unsafeScript[0] = opcode;
            Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(analysis, 1, [0x40], unsafeScript), "A branch, input, interactive or unknown instruction authenticated autonomous preparation.");
        }
        Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(analysis, 1, [0x40], [0x03, 0, 0, 0x6D, 6, 0, 0, 0]), "A recursive request closure authenticated finite preparation.");
        bool waiting = true;
        for (int i = 0; i <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; i++)
        {
            waiting = readiness.ShouldWaitForNavigationPreparation(1, "source", vm with { Tick = i }, process, script, (_, _) => analysis);
        }
        Check(!waiting, "Frozen admitted work prevented physical planning indefinitely.");
        Check(readiness.ShouldWaitForNavigationPreparation(2, "new-source", vm, process, script, (_, _) => analysis), "A new actual source inherited exhausted readiness observations.");
        Ff7AutoPlayNativeReadiness cameraReadiness = new();
        for (int step = 0; step < 80; step++)
        {
            Check(cameraReadiness.ShouldWaitForNavigationPreparation(1, "source", vm, process with { CameraScroll = new(6, 0, 0, 0, 100, 100, step) }, script, (_, _) => analysis), "A genuinely advancing native camera wait was mistaken for frozen preparation.");
        }
        Check(before == JsonSerializer.Serialize(new { vm, process }), "Readiness changed native facts.");
        RunOriginal();
        Console.WriteLine("PASS admitted autonomous navigation preparation");
    }

    private static void CheckFadePrefix()
    {
        byte[] script = [0x6B, 0, 0, 0, 0, 0, 8, 2, 0, 0x6C, 0x64, 0, 0, 0, 9, 1, 0x6D, 6, 0, 0, 0];
        FieldScriptAnalysis analysis = new(true, null, "finite-fade-prelude", false, [], [0, 9, 10, 16, 20], [])
        {
            MayChangeNavigation = true,
            UnblockedTriangles = [6],
            InstructionSites = [new(0, 0), new(0, 9), new(0, 10), new(0, 16), new(0, 20)]
        };
        Check(Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(analysis, 1, [0x40], script), "A finite literal fade/camera prefix was rejected.");
        foreach ((int index, byte value) in new (int, byte)[] { (1, 0x10), (2, 1), (6, 0), (7, 7), (11, 0x10) })
        {
            byte[] unsupported = (byte[])script.Clone();
            unsupported[index] = value;
            Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(analysis, 1, [0x40], unsupported), "A variable, zero-speed, persistent tint or unsupported camera qualified finite preparation.");
        }
        FieldScriptAnalysis remaining = analysis with { InstructionSites = [new(0, 9), new(0, 10), new(0, 16), new(0, 20)] };
        Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(remaining, 1, [0x40], script), "An unpaired future fade wait qualified through unrelated work.");
        Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(analysis with { InstructionSites = [new(1, 0), new(0, 9), new(0, 10), new(0, 16), new(0, 20)] }, 2, [0x40], script), "A different entity's fade authenticated a future wait.");
        byte[] childEntries = [0x02, 1, 1, 0x02, 1, 2, 0, .. script];
        FieldScriptAnalysis childAnalysis = analysis with { InstructionSites = [new(0, 0), new(0, 3), new(0, 6), new(1, 7), new(1, 16), new(1, 17), new(1, 23), new(1, 27)] };
        Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(childAnalysis, 2, [0x40], childEntries, rootEntity: 0), "A later child-entry producer authenticated an earlier requested child wait.");
        int[] saved = new int[8];
        saved[7] = 20;
        FieldEntitySnapshot owner = new(9, 6, 0, saved, new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "finite-fade-prelude", 1, true, new byte[1280], new byte[256], [owner]);
        Ff7FieldProcess process = new() { BlockedTriangles = [0x40] };
        Ff7AutoPlayNativeReadiness readiness = new();
        for (int position = 0; position < 80; position++)
        {
            Ff7FadeProcess fade = new(2, position, 1, 0, 0, 0, false);
            Check(readiness.ShouldWaitForNavigationPreparation(1, "source", vm, process with { FadeState = fade }, script, (_, _) => remaining), "Actual fade advancement was mistaken for a frozen native wait.");
        }
        foreach (Ff7FadeProcess fade in new[] { new Ff7FadeProcess(7, 20, 1, 0, 0, 0, false), new Ff7FadeProcess(2, 0, 0, 0, 0, 0, false) })
        {
            Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(remaining, 1, [0x40], script, vm, fade), "A current persistent or zero-speed fade authenticated waiting.");
        }
        Check(!Ff7AutoPlayNativeReadiness.IsAutonomousNavigationPreparation(remaining, 1, [0x40], script, vm with { Entities = [owner with { Priority = 7 }] }, new(2, 1, 1, 0, 0, 0, false)), "An ambient owner inherited finite fade waiting.");
    }

    private static void RunOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NAVIGATION_PREPARATION_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP external native navigation preparation continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before navigation preparation completes."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        int sourceMap = session.MapId;
        int callbacks = 0;
        bool unlocked = false;
        int contacts = 0;
        for (; callbacks < 256 && controller.Running; callbacks++)
        {
            controller.Advance(1);
            if (session.MapId != sourceMap)
            {
                break;
            }
            unlocked = (session.Host.Process.BlockedTriangles[0] & 0x40) == 0;
            if (unlocked)
            {
                Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                contacts = Ff7AutoplayDriver.DiscoverLineContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, session.Host.Process.Lines[0]).Count;
                break;
            }
            Check(!controller.PlanningProgress.Pending, "Physical or native graph search held a proved autonomous navigation prelude.");
        }
        Check(controller.Error is null && unlocked && contacts > 0, $"Ordinary policy did not complete its navigation prelude: {controller.Error}; callbacks={callbacks}; contacts={contacts}");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The external standalone source changed.");
        Console.WriteLine($"PASS external navigation preparation: {callbacks + 1} callbacks, tick {session.Machine.Tick}, {contacts} native line contacts");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
