using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSectionEntryOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SECTION_ENTRY_SAVE") ?? throw new InvalidOperationException("Select a native section-entry fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        for (int poll = 0; poll < 16 && controller.Error is null && (session.MapId != 650 || session.Machine.Tick < 4); poll++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Section-relative entry continuation: field={session.Field.Name}, tick={session.Machine.Tick}, callbacks={controller.Steps}, locked={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, error={controller.Error}");
        if (session.MapId != 650 || session.Machine.Tick < 4 || controller.Error is not null || session.Field.Scripts.Entities[12].ScriptOffsets[3] != session.Field.Scripts.TextOffset || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native accepted field transition did not retain its literal section-relative entries and initialize normally.");
        }
    }
}
