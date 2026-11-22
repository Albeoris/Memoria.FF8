using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMateriaPrerequisiteTests
{
    internal static void Run()
    {
        IReadOnlyList<IReadOnlyList<byte>> groups = new byte[][] { [1, 2], [3], [4], [5, 6] };
        byte[] record = Enumerable.Repeat((byte)255, 132).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 0xFFFFFF01);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(124), 0xFFFFFF02);
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [new(0, "A", 1, 0, 1, 1, record)], Party = [0, 255, 255], Inventory = [], Materia = [], AvailableCharacters = 1 };
        string original = JsonSerializer.Serialize(progress);
        Check(Ff7NativeMateriaPrerequisites.Evaluate(progress, 0, groups), "Mastered weapon and last armor slots did not satisfy their native group.");
        Check(!Ff7NativeMateriaPrerequisites.Evaluate(progress with { AvailableCharacters = 0 }, 0, groups), "An unavailable character's materia satisfied the query.");
        byte[] notMastered = record.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(notMastered.AsSpan(64), 0xFFFFFE01);
        Check(!Ff7NativeMateriaPrerequisites.Evaluate(progress with { Characters = [progress.Characters[0] with { NativeRecord = notMastered }] }, 0, groups), "Equipped AP below the exact mastery sentinel satisfied the query.");
        Check(!Ff7NativeMateriaPrerequisites.Evaluate(progress with { Characters = [], AvailableCharacters = 0, Materia = [new(1, 0xFFFFFF), new(1, 0xFFFFFF), new(2, 0xFFFFFE)] }, 0, groups), "Duplicates or nearly mastered AP concealed a missing mastered ID.");
        Check(Ff7NativeMateriaPrerequisites.Evaluate(progress with { Characters = [], AvailableCharacters = 0, Materia = [new(1, 1), new(2, 2), new(1, 0xFFFFFF), new(2, 0xFFFFFF)] }, 0, groups), "A first nonmastered instance hid a later mastered instance.");
        foreach (int parameter in new[] { 1, 2 })
        {
            Check(!Ff7NativeMateriaPrerequisites.Evaluate(progress, parameter, groups), "A missing native group incorrectly passed.");
            Check(Ff7NativeMateriaPrerequisites.Evaluate(progress with { Materia = [new(groups[parameter][0], 0xFFFFFF)] }, parameter, groups), "A mastered inventory instance failed its selected group.");
        }
        Ff7Progress present = progress with { Materia = [new(5, 0), new(6, 1)] };
        Check(Ff7NativeMateriaPrerequisites.Evaluate(present, 3, groups) && !Ff7NativeMateriaPrerequisites.Evaluate(present with { Materia = [new(5, 0)] }, 3, groups), "The native presence-only group incorrectly required mastery or ignored an absent ID.");
        Check(Ff7NativeMateriaPrerequisites.Evaluate(progress, 4, groups) && Ff7NativeMateriaPrerequisites.Evaluate(progress, 255, groups), "The native out-of-switch selector must publish success.");
        Check(original == JsonSerializer.Serialize(progress), "The native prerequisite query changed progress or equipped materia.");
        Ff7FieldResources field = new("materia-query-fixture", null!, null!, null!, [], [], null!, null!, null!);
        Ff7FieldHost host = new(field, (_, _) => 1, new() { BlinkStateInitialized = true });
        int applications = 0;
        host.ExternalCommand = command =>
        {
            applications++;
            Check(command == new FieldMenuCommand(22, 0), "The global process changed the original query selector.");
            return new(true);
        };
        FieldHostRequest request = new(7, 0, 1, 4, new FieldMenuCommand(22, 0));
        Check(!host.Execute(request).Completed && applications == 0, "MENU22 must queue and yield before its native global service.");
        host.AdvanceNativeMenu();
        host.AdvanceNativeMenu();
        Check(applications == 1 && host.Execute(request).Completed && host.Process.GlobalCommand == Ff7GlobalCommand.None, "The native query did not service exactly once and release its MENU owner.");
        Console.WriteLine("PASS native materia prerequisite selectors, exact mastery/presence and automatic menu lifecycle.");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MATERIA_QUERY_SAVE") ?? throw new InvalidOperationException("Select an external native materia-query fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using Ff7Session service = Ff7Session.Restore(installation, source);
        FieldHostRequest request = source.Vm.PendingRequests!.Single(request => request.Command is FieldMenuCommand { Mode: 22 });
        Check(!service.Host.Execute(request).Completed && JsonSerializer.Serialize(service.Progress) == JsonSerializer.Serialize(source.Progress), "Queuing the actual native query prematurely changed progression.");
        FieldVmSnapshot before = service.Machine.Capture();
        service.Host.AdvanceNativeMenu();
        FieldVmSnapshot after = service.Machine.Capture();
        bool expected = Ff7NativeMateriaPrerequisites.Evaluate(source.Progress, ((FieldMenuCommand)request.Command).Parameter, Ff7PcExecutableData.Load(installation).NativeMateriaRequirementGroups);
        Check(after.PersistentVariables[0x46F] == (expected ? 1 : 0), "The actual native query published a wrong prerequisite result.");
        Check(JsonSerializer.Serialize(before with { PersistentVariables = after.PersistentVariables }) == JsonSerializer.Serialize(after) && before.PersistentVariables.Where((value, index) => index != 0x46F && value != after.PersistentVariables[index]).Count() == 0, "Native query service changed another variable, script PC, RNG or execution owner.");
        Check(JsonSerializer.Serialize(service.Progress) == JsonSerializer.Serialize(source.Progress), "Native prerequisite service consumed materia or changed progression.");
        using Ff7Session warm = Ff7Session.Restore(installation, source);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(source))!);
        int callbacks = 0;
        while (callbacks++ < 8 && warm.Machine.Capture().PendingRequests!.Any(pending => pending.Id == request.Id))
        {
            warm.AdvanceTick(advanceClock: false);
            cold.AdvanceTick(advanceClock: false);
            Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The actual native query changed independent cold continuation.");
        }
        Check(!warm.Machine.Capture().PendingRequests!.Any(pending => pending.Id == request.Id) && JsonSerializer.Serialize(warm.Progress) == JsonSerializer.Serialize(source.Progress), "The native MENU22 request failed normal acknowledgement or changed inventory/RNG.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The actual native query changed its source standalone.");
        Console.WriteLine($"PASS original MENU22: entity={request.Entity}, priority={request.Priority}, pc={request.ProgramCounter:X4}, result={expected}, tick={source.Process.Tick}->{warm.Host.Process.Tick}, cold=exact, source=unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
