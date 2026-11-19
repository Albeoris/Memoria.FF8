using System.Diagnostics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeKeymanRouteTests
{
    internal static void Run()
    {
        Check(Ff7AutoPlayPolicy.ReadKeymanProgressionEvent(618) == "line:9:entity:15:script:2" && Ff7AutoPlayPolicy.ReadKeymanProgressionEvent(626) == "line:9:entity:15:script:2", "The key route lost its pre-mural native door.");
        Check(Ff7AutoPlayPolicy.ReadKeymanProgressionEvent(627) == "gateway:0", "The completed mural phase must return through the clock gateway instead of entering the mural again.");
        Check(Ff7AutoPlayPolicy.ReadTempleReturnDestination(610, 627, "E81D4EC5D4FE755D7E214DCDD225C4D86061A7DDDEC5A0827E7DCB2C48968945") == 607, "The mural return must retain its clock destination even when that gateway was consumed on the earlier visit.");
        Check(Ff7AutoPlayPolicy.ReadTempleReturnDestination(607, 627, "990BE7C79174E0B781F19BECE4C986E452CD15247A975847624305ECC7FD7669") == 616, "The initialized return bridge must lead to XII.");
        Check(Ff7AutoPlayPolicy.ReadTempleReturnDestination(610, 618, "E81D4EC5D4FE755D7E214DCDD225C4D86061A7DDDEC5A0827E7DCB2C48968945") is null && Ff7AutoPlayPolicy.ReadTempleReturnDestination(610, 627, "unknown") is null, "The return profile must not claim an earlier phase or an unrecognized native program.");
        int[] logicalDoors = [4, 5, 3, 0, 2, 7, 1, 6, 8];
        int[] physicalExits = [3, 6, 8, 1, 5, 2, 7, 4, 0];
        int[] floors = [1, 0x1E, 0x1E0];
        foreach (int floor in floors)
        {
            for (int initialNpc = 0; initialNpc < 9; initialNpc++)
            {
                int[] plan = Ff7AutoPlayPolicy.ReadKeymanDoorPlan(initialNpc, floor);
                Check(plan.Length > 0, "A finite native door state failed to produce an interception plan.");
                int available = floor;
                int npc = initialNpc;
                for (int step = 0; step < plan.Length; step++)
                {
                    int entry = plan[step];
                    Check((available & (1 << entry)) != 0, "A door plan selected an entrance on another native floor.");
                    bool caught = logicalDoors[entry] == npc;
                    Check(caught == (step == plan.Length - 1), "A door plan missed its final interception or continued after catching the keyman.");
                    int exit = physicalExits[entry];
                    available = exit == 0 ? 1 : exit <= 4 ? 0x1E : 0x1E0;
                    npc = (npc + 1) % 9;
                }
                Check(!CanCatchInFewerSteps(floor, initialNpc, plan.Length, logicalDoors, physicalExits), "A door plan used more transports than the finite product graph requires.");
            }
        }
        int[] current = Ff7AutoPlayPolicy.ReadKeymanDoorPlan(2, 0x1E0);
        Check(current.SequenceEqual([5, 2]), "The lower-floor NPC2 state must first miss through LINE5, then intercept NPC3 through LINE2.");
        Check(Ff7AutoPlayPolicy.ReadKeymanDoorPlan(3, 0x1E).SequenceEqual([2]), "The actual NPC advance after the first miss must renew the preferred entrance.");
        Check(Ff7AutoPlayPolicy.ReadKeymanDoorPlan(2, 0x1E).SequenceEqual([4]), "The matching entrance can only be selected when its native floor is actually available.");
        Check(Ff7AutoPlayPolicy.ReadKeymanDoorPlan(2, 0).Length == 0, "An empty actual contact catalog must not invent a first transport.");
        Check(Ff7AutoPlayPolicy.ReadKeymanDoorPlan(2, 1 << 7).SequenceEqual([7, 2]), "A restricted actual contact mask must be respected before following declared native exit floors.");
        Check(Ff7AutoPlayPolicy.ReadKeymanExitPlan(1, true).SequenceEqual([9]), "An actually reachable unlocked progression entrance must be preferred directly.");
        Check(Ff7AutoPlayPolicy.ReadKeymanExitPlan(0x1E, false).SequenceEqual([1, 8, 9]), "The middle floor requires exactly two native transports before progression LINE9.");
        Check(Ff7AutoPlayPolicy.ReadKeymanExitPlan(0x1E0, false).SequenceEqual([8, 9]), "The lower floor must take its direct upper-floor transport before progression LINE9.");
        Check(Ff7AutoPlayPolicy.ReadKeymanExitPlan(1 << 2, false).SequenceEqual([2, 8, 9]), "Post-catch planning must respect a restricted actual first-contact mask.");
        Check(Ff7AutoPlayPolicy.ReadKeymanExitPlan(0, false).Length == 0, "Post-catch planning must not invent a transport when there are no actual contacts.");
        foreach (int floor in floors)
        {
            int[] plan = Ff7AutoPlayPolicy.ReadKeymanExitPlan(floor, floor == 1);
            int available = floor;
            for (int step = 0; step < plan.Length; step++)
            {
                int entry = plan[step];
                if (entry == 9)
                {
                    Check(available == 1 && step == plan.Length - 1, "Progression was selected outside its unlocked upper native floor or before the final step.");
                    continue;
                }
                Check((available & (1 << entry)) != 0, "A post-catch route selected another floor's entrance.");
                int exit = physicalExits[entry];
                available = exit == 0 ? 1 : exit <= 4 ? 0x1E : 0x1E0;
            }
            Check(plan.Length == (floor == 1 ? 1 : floor == 0x1E0 ? 2 : 3), "The post-catch route exceeded its minimum native transport count.");
        }
        Console.WriteLine("PASS native keyman 27 product states, minimum transport plans, actual first-contact mask and moving interception target");
        Console.WriteLine("PASS native keyman unlocked floor routes, minimum transports and actual progression contacts");
    }

    private static bool CanCatchInFewerSteps(int available, int npc, int length, int[] logicalDoors, int[] physicalExits)
    {
        if (length <= 1)
        {
            return false;
        }
        for (int entry = 0; entry < 9; entry++)
        {
            if ((available & (1 << entry)) == 0)
            {
                continue;
            }
            if (logicalDoors[entry] == npc)
            {
                return true;
            }
            int exit = physicalExits[entry];
            int nextAvailable = exit == 0 ? 1 : exit <= 4 ? 0x1E : 0x1E0;
            if (CanCatchInFewerSteps(nextAvailable, (npc + 1) % 9, length - 1, logicalDoors, physicalExits))
            {
                return true;
            }
        }
        return false;
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_KEYMAN_SAVE") ?? throw new InvalidOperationException("Select an external keyman fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), source);
        bool alreadyCaught = (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xE9) & 8) != 0;
        bool returning = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) >= 627;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The required interception must not explore optional choices."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        List<int> entries = [];
        session.NativeInteractionScriptRequested += admission =>
        {
            if (admission.Accepted && admission.Started && admission.Entity is >= 6 and <= 14 && admission.Script == 5)
            {
                entries.Add(admission.Entity - 6);
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        ushort tracedField = session.MapId;
        while ((session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xE9) & 8) == 0 && controller.Running && session.Battle is null && timer.Elapsed < TimeSpan.FromSeconds(60))
        {
            controller.Advance(1);
        }
        Check((session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xE9) & 8) != 0 && controller.Error is null, $"Native interception did not unlock the key route: {controller.Error}; status={policy.Status}; steps={controller.Steps}; entries={string.Join(',', entries)}.");
        Check(entries.SequenceEqual(alreadyCaught ? Array.Empty<int>() : new[] { 5, 2 }), $"The actual route did not use the minimum two native transports: {string.Join(',', entries)}.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Interception changed its authoritative source file.");
        Console.WriteLine($"PASS original two-transport native interception [{string.Join(',', entries)}], key flag and unchanged source; steps={controller.Steps}; elapsed={timer.Elapsed.TotalSeconds:F3}s");
        while ((session.MapId == 610 || returning && session.MapId == 607) && controller.Running && session.Battle is null && policy.State.PendingRestore is null && timer.Elapsed < TimeSpan.FromSeconds(60))
        {
            controller.Advance(1);
            if (tracedField != session.MapId)
            {
                Console.WriteLine($"Native phase route {tracedField}->{session.MapId}; story={session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}; goal={policy.State.NavigationGoal?.EventId}; destination={policy.State.DestinationField}");
                tracedField = session.MapId;
            }
        }
        var finalVm = session.Machine.Capture();
        string menuState = System.Text.Json.JsonSerializer.Serialize(session.Host.Process.Menu);
        if (alreadyCaught && policy.State.PendingRestore is { } restore)
        {
            Check(session.Host.Process.Menu is { Mode: 14, Completed: true, SaveOutcome: Ff7SaveMenuOutcome.Cancelled }, "The native save menu did not record its real cancellation before the choice restore.");
            Check(finalVm.PendingRequests?.Any(request => request.Command is FieldMenuCommand { Mode: 14 }) != true && File.Exists(restore.Path), "The native MENU request is still pending or its retained choice dependency is missing.");
            Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Native menu continuation changed its source file.");
            Console.WriteLine($"PASS native save cancellation and original MENU continuation; retained choice restore required; callbacks={controller.Steps}; source unchanged");
            return;
        }
        int expectedField = returning ? 616 : 611;
        string pc27 = finalVm.Entities.Length > 27 ? finalVm.Entities[27].ProgramCounter.ToString("X4") : "absent";
        Check(session.MapId == expectedField && controller.Error is null, $"The unlocked keyman route did not reach its required native exit: {controller.Error}; field={session.MapId}; status={policy.Status}; entries={string.Join(',', entries)}; pc27={pc27}; pendingTick={session.HasPendingTick}; restore={policy.State.PendingRestore}; menu={menuState}.");
        Check(entries.SequenceEqual(returning ? new[] { 8 } : alreadyCaught ? new[] { 1, 8 } : new[] { 5, 2, 1, 8 }), $"The actual catch and departure did not use the minimum native transports: {string.Join(',', entries)}.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The keyman departure changed its authoritative source file.");
        Console.WriteLine($"PASS original catch plus ordinary progression to field{expectedField}; entries=[{string.Join(',', entries)}]; steps={controller.Steps}; elapsed={timer.Elapsed.TotalSeconds:F3}s");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
