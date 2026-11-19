using System.Diagnostics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeKeymanRouteTests
{
    internal static void Run()
    {
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
        Console.WriteLine("PASS native keyman 27 product states, minimum transport plans, actual first-contact mask and moving interception target");
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
        while ((session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xE9) & 8) == 0 && controller.Running && session.Battle is null && timer.Elapsed < TimeSpan.FromSeconds(60))
        {
            controller.Advance(1);
        }
        Check((session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xE9) & 8) != 0 && controller.Error is null, $"Native interception did not unlock the key route: {controller.Error}; status={policy.Status}; steps={controller.Steps}; entries={string.Join(',', entries)}.");
        Check(entries.SequenceEqual(new[] { 5, 2 }), $"The actual route did not use the minimum two native transports: {string.Join(',', entries)}.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Interception changed its authoritative source file.");
        Console.WriteLine($"PASS original two-transport native interception [{string.Join(',', entries)}], key flag and unchanged source; steps={controller.Steps}; elapsed={timer.Elapsed.TotalSeconds:F3}s");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
