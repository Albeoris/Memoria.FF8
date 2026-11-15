using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWindowDefaultsTests
{
    public static void Run()
    {
        CheckDefaults(new Ff7FieldProcess());
        CheckLegacyWindows();
        CheckPendingMessage();
        CheckOriginalCheckpoint();
    }

    private static void CheckLegacyWindows()
    {
        Ff7FieldResources field = NativeDialogueTests.Field();
        Ff7FieldHost initial = new(field, (_, _) => 1);
        Ff7FieldProcess legacy = initial.Capture() with { Windows = [] };
        Ff7FieldHost restored = new(field, (_, _) => 1, Cold(legacy));
        CheckDefaults(restored.Process);
        Check(JsonSerializer.Serialize(legacy with { Windows = restored.Process.Windows }) == JsonSerializer.Serialize(restored.Capture()), "Filling missing legacy window slots changed unrelated native process state.");

        Ff7Window configured = new(1, 33, 44, 188, 52, 2, 3, 2)
        {
            NativeNumber = 12345,
            NativeNumberDigits = 3,
            SpecialType = 2,
            SpecialX = 17,
            SpecialY = 19,
            Parameters = new Ff7WindowParameters().Set(0, new(FieldBank.MapWord, 12))
        };
        Ff7FieldHost partial = new(field, (_, _) => 1, Cold(legacy with { Windows = [configured] }));
        Check(partial.Process.Windows.Length == 4 && partial.Process.Windows.Single(window => window.Id == 1) == configured, "Restoring missing native slots overwrote an explicitly configured window.");
        foreach (int id in new[] { 0, 2, 3 })
        {
            Check(partial.Process.Windows.Single(window => window.Id == id) == Ff7Window.CreateDefault(id), "A missing legacy window slot did not receive its native defaults.");
        }
        Ff7FieldHost cold = new(field, (_, _) => 1, Cold(partial.Capture()));
        Check(JsonSerializer.Serialize(partial.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold restoration changed configured native window geometry or parameters.");
        Ff7FieldHost nextField = new(field, (_, _) => 1, partial.CreateNextFieldProcess());
        Check(nextField.Process.Windows.Length == 4, "A field transition did not allocate all native window slots.");
        Check(nextField.Process.Windows.Single(window => window.Id == 1) == (Ff7Window.CreateDefault(1) with { NativeNumber = configured.NativeNumber }), "A field transition failed to reset window configuration while retaining the native numeric register.");
        foreach (int id in new[] { 0, 2, 3 })
        {
            Check(nextField.Process.Windows.Single(window => window.Id == id) == Ff7Window.CreateDefault(id), "A field transition retained obsolete configuration in an ordinary native window slot.");
        }
    }

    private static void CheckPendingMessage()
    {
        Ff7FieldResources field = NativeDialogueTests.Field([0x21, 255]);
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldProgram program = new([0, 0x40, 1, 0, 0], [entries]);
        Ff7FieldHost host = new(field, (_, _) => 1, new Ff7FieldProcess { Windows = [] });
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        Check(machine.TryRequestScript(0, 1), "The native message fixture could not enter its ordinary interaction script.");
        machine.AdvanceTick();
        FieldVmSnapshot vm = machine.Capture();
        FieldHostRequest pending = vm.PendingRequests!.Single();
        Check(pending.Command is FieldDialogueCommand { Window: 1, Text: 0 } && host.Process.Dialogues.Single().RequestId == pending.Id, "MESSAGE without WINDOW did not use the native default slot or retain its pending request owner.");
        Ff7FieldProcess process = host.Capture();
        string beforeVm = JsonSerializer.Serialize(vm);
        Ff7FieldHost coldHost = new(field, (_, _) => 1, Cold(process with { Windows = [] }));
        FieldMachine coldMachine = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(beforeVm)!, coldHost);
        Check(beforeVm == JsonSerializer.Serialize(coldMachine.Capture()), "Filling missing windows replayed or changed the pending message VM request.");
        Check(JsonSerializer.Serialize(process.Dialogues) == JsonSerializer.Serialize(coldHost.Process.Dialogues) && JsonSerializer.Serialize(process.Receipts) == JsonSerializer.Serialize(coldHost.Capture().Receipts), "Filling missing windows changed native message presentation or host receipts.");
        Check(coldMachine.Capture().PendingRequests!.Single().Id == pending.Id && coldHost.Process.Dialogues.Single().RequestId == pending.Id, "Cold default-window restoration allocated a replacement message request.");
        CheckDefaults(coldHost.Process);
    }

    private static void CheckOriginalCheckpoint()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WINDOW_DEFAULT_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original default-window continuation: configure original resources and an external standalone message fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The window fixture requires an FFVII continuation.");
        FieldHostRequest[] pending = saved.Vm.PendingRequests?.Where(request => request.Command is FieldDialogueCommand).ToArray() ?? [];
        Check(pending.Length > 0, "The configured default-window fixture has no pending native message request.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        Check(JsonSerializer.Serialize(saved.Vm) == JsonSerializer.Serialize(session.Machine.Capture()), "Restoring native default windows changed pending script PCs or request ownership.");
        Check(JsonSerializer.Serialize(saved.Progress) == JsonSerializer.Serialize(session.Progress), "Restoring native default windows changed recorded gameplay progress.");
        foreach (FieldHostRequest request in pending)
        {
            int window = ((FieldDialogueCommand)request.Command).Window;
            Check(session.Host.Process.Windows.Count(item => item.Id == window) == 1, "A pending original MESSAGE has no restored native window slot.");
        }
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The default-window restoration changed its external standalone source.");
    }

    private static Ff7FieldProcess Cold(Ff7FieldProcess process)
    {
        return JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(process))!;
    }

    private static void CheckDefaults(Ff7FieldProcess process)
    {
        Check(process.Windows.Length == 4 && process.Windows.Select(window => window.Id).Order().SequenceEqual(new[] { 0, 1, 2, 3 }), "A field allocation did not initialize exactly four distinct native window slots.");
        foreach (Ff7Window window in process.Windows)
        {
            Check(window.X == 8 && window.Y == (window.Id == 1 ? 8 : 149) && window.Width == 304 && window.Height == 74 && window.Style == 0 && window.Flags == 0 && window.Rows == 0 && window.Parameters is null && window.SpecialType == 0 && window.NativeNumber == 0 && window.NativeNumberDigits == 6, "An initial native window has incorrect geometry, display flags or numeric defaults.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
