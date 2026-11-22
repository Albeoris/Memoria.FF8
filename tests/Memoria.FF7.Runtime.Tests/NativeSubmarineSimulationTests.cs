using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSubmarineSimulationTests
{
    internal static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native submarine simulation: configure selected original resources.");
            return;
        }
        Ff7SubmarineResources resources = Ff7SubmarineResources.Load(Ff7Installation.Discover(root));
        FieldHostRequest request = new(7, 0, 1, 20, new FieldMinigameCommand(new(324, 0, 0, 0, 0), 4));
        Ff7SubmarineRuntime seeded = new(resources, request, 0, 1);
        Ff7SubmarineSnapshot initialized = seeded.Capture();
        int mines = Enumerable.Range(0, 4096).Count(cell => (initialized.Cells[cell * 8 + 5] & 0x2000) != 0);
        Check(initialized.RandomSeed == 0xA360B40A && mines == resources.InitialGlobals[0x988504], "Native initialization changed CRT reseeding, mine count or shared RNG draw order.");
        Ff7SubmarineRuntime runtime = new(resources, request, 0, 12345);
        Ff7SubmarineSnapshot source = runtime.Capture();
        string before = JsonSerializer.Serialize(source);
        Ff7SubmarineRuntime cold = new(resources, request, 0, source.RandomSeed, JsonSerializer.Deserialize<Ff7SubmarineSnapshot>(before)!);
        for (int tick = 0; tick < 8; tick++)
        {
            runtime.Advance(0, 0);
            cold.Advance(0, 0);
            Check(JsonSerializer.Serialize(runtime.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Submarine simulation diverged after a cold JSON restore.");
        }
        Check(before == JsonSerializer.Serialize(source), "Advancing a restored module changed the supplied snapshot arrays.");
        Ff7SubmarineSnapshot detached = runtime.Capture();
        detached.Player[0] ^= 1;
        detached.Ships[0][0] ^= 1;
        detached.Torpedoes[0][0] ^= 1;
        detached.Cells[0] ^= 1;
        detached.Globals.Clear();
        Check(JsonSerializer.Serialize(runtime.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Capture exposed mutable live submarine allocations.");
        Reject(() => new Ff7SubmarineRuntime(resources, request with { Id = 8 }, 0, 1, source));
        Reject(() => new Ff7SubmarineRuntime(resources, request, 0, 1, source with { Ships = [] }));
        Reject(() => new Ff7SubmarineRuntime(resources, request, 0, 1, source with { Torpedoes = [] }));
        Dictionary<int, int> wrongTotal = new(source.Globals) { [0xE981C0] = source.Globals[0xE981C0] + 1 };
        Reject(() => new Ff7SubmarineRuntime(resources, request, 0, 1, source with { Globals = wrongTotal }));
        Reject(() => new Ff7SubmarineRuntime(resources, request, 0, 1, source with { Phase = Ff7SubmarinePhase.Completed }));
        CheckGuidance(runtime);
        CheckShips(resources, request, source);
        CheckDamage(resources, request, source, playerTarget: false);
        CheckDamage(resources, request, source, playerTarget: true);
        Console.WriteLine("Native submarine vectors, cold simulation, ownership and causal torpedo outcomes passed.");
    }

    private static void CheckGuidance(Ff7SubmarineRuntime runtime)
    {
        int[] torpedo = new int[0xC8 / 4];
        torpedo[4 / 4] = 100 << 12;
        Word(torpedo, 0x40, 512);
        Word(torpedo, 0x42, 512);
        torpedo[0x70 / 4] = 20000;
        torpedo[0x6C / 4] = 40000;
        Invoke(runtime, "AdvanceTorpedoGuidance", torpedo);
        Check(ReadWord(torpedo, 0x20) == 32 && ReadWord(torpedo, 0x22) == 32 && ReadWord(torpedo, 0x24) == 256, "Native torpedo guidance angle interpolation changed.");
        Check(Vector(torpedo, 0x28).SequenceEqual(new[] { 3783, 1565, -108 }) && Vector(torpedo, 0x30).SequenceEqual(new[] { -1556, 3779, 260 }) && Vector(torpedo, 0x38).SequenceEqual(new[] { 199, -200, 4086 }), "Native mode-one torpedo basis differs from its execution vector.");
        Check(torpedo[0x68 / 4] == 2282 && torpedo[0x80 / 4] == 2080 && torpedo[0x70 / 4] == 25000, "Native guidance dot, cell or speed changed.");
        Array.Clear(torpedo);
        Word(torpedo, 0x20, 2040);
        Word(torpedo, 0x22, -2040);
        Word(torpedo, 0x40, -2040);
        Word(torpedo, 0x42, 2040);
        torpedo[0x70 / 4] = 20000;
        torpedo[0x6C / 4] = 19993;
        Invoke(runtime, "AdvanceTorpedoGuidance", torpedo);
        Check(ReadWord(torpedo, 0x20) == 2041 && ReadWord(torpedo, 0x22) == -2041 && torpedo[0x68 / 4] == 4090 && torpedo[0x70 / 4] == 19999, "Signed angle wrap or negative-speed correction changed.");
    }

    private static void CheckShips(Ff7SubmarineResources resources, FieldHostRequest request, Ff7SubmarineSnapshot initial)
    {
        Ff7SubmarineSnapshot source = JsonSerializer.Deserialize<Ff7SubmarineSnapshot>(JsonSerializer.Serialize(initial))!;
        source.Player[0] = resources.Player.X << 18;
        source.Player[1] = resources.Player.Y << 18;
        source.Player[2] = resources.Player.Z << 18;
        Word(source.Player, 0x1C, 4096);
        Word(source.Player, 0x1E, 0);
        Word(source.Player, 0x20, 0);
        Word(source.Player, 0x24, 0);
        Word(source.Player, 0x26, 4096);
        Word(source.Player, 0x28, 0);
        Word(source.Player, 0x2C, 0);
        Word(source.Player, 0x2E, 0);
        Word(source.Player, 0x30, 4096);
        source.Globals[0x987020] = 0;
        source.Globals[0xEC1824] = 1;
        source.Globals[0xEC1828] = 0;
        Ff7SubmarineRuntime runtime = new(resources, request, 0, source.RandomSeed, source);
        Invoke(runtime, "AdvanceShips");
        int[] ship = runtime.Capture().Ships[0];
        Check(ship[0] == resources.Fleet[0].X << 18 && ship[1] == resources.Fleet[0].Y << 18 && ship[2] == unchecked((resources.Fleet[0].Z << 18) + resources.Fleet[0].Speed), "Native ship integration changed its resource-derived initial displacement.");
        Check(ship[0x2C / 4] == 2012 && ship[0x30 / 4] == 14000 && ship[0x34 / 4] == 25 && ship[0x10 / 4] == 6000 && ReadWord(ship, 0x18) == 4096, "Native ship acceleration, mode or health vector changed.");
    }

    private static void CheckDamage(Ff7SubmarineResources resources, FieldHostRequest request, Ff7SubmarineSnapshot initial, bool playerTarget)
    {
        Ff7SubmarineSnapshot source = JsonSerializer.Deserialize<Ff7SubmarineSnapshot>(JsonSerializer.Serialize(initial))!;
        foreach (int[] ship in source.Ships)
        {
            Array.Clear(ship);
        }
        source.Ships[0][0x34 / 4] = 1;
        source.Ships[0][0x10 / 4] = playerTarget ? 6000 : 512;
        source.Ships[0][1] = 100 << 12;
        source.Player[0] = 0;
        source.Player[1] = 100 << 12;
        source.Player[2] = 0;
        source.Player[0x10 / 4] = playerTarget ? 512 : 16384;
        source.Globals[0xE981C4] = source.Globals[0xE981C0] - 1;
        source.Globals[0xE981C8] = 0;
        for (int cell = 0; cell < 4096; cell++)
        {
            source.Cells[cell * 8 + 5] = 0;
            source.Cells[cell * 8 + 7] = 1024;
        }
        int[] torpedo = source.Torpedoes[playerTarget ? 16 : 0];
        Array.Clear(torpedo);
        torpedo[1] = 100 << 12;
        torpedo[0x14 / 4] = 100 << 12;
        torpedo[0x74 / 4] = 48;
        torpedo[0x78 / 4] = 32;
        torpedo[0x7C / 4] = 2;
        torpedo[0x84 / 4] = playerTarget ? -1 : 0;
        torpedo[0x88 / 4] = 5;
        Ff7SubmarineRuntime runtime = new(resources, request, 0, source.RandomSeed, source);
        Invoke(runtime, "AdvanceTorpedoes");
        Ff7SubmarineSnapshot hit = runtime.Capture();
        Check((playerTarget ? hit.Player : hit.Ships[0])[0x10 / 4] == 0 && hit.Torpedoes[playerTarget ? 16 : 0][0x7C / 4] == 32 && !runtime.Finished, "An actual native torpedo collision did not cause damage independently of mission completion.");
        runtime.Advance(0, 0);
        Check(runtime.Phase == Ff7SubmarinePhase.Result && runtime.Frame.Outcome == (playerTarget ? 2 : 1), "Causal native damage did not select the corresponding mission result.");
    }

    private static void Invoke(Ff7SubmarineRuntime runtime, string method, params object[] args) => typeof(Ff7SubmarineRuntime).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runtime, args);
    private static int ReadWord(int[] data, int offset) => unchecked((short)(data[offset / 4] >> ((offset & 2) * 8)));
    private static int[] Vector(int[] data, int offset) => [ReadWord(data, offset), ReadWord(data, offset + 2), ReadWord(data, offset + 4)];

    private static void Word(int[] data, int offset, int value)
    {
        int shift = (offset & 2) * 8;
        data[offset / 4] = data[offset / 4] & ~(65535 << shift) | (value & 65535) << shift;
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("A submarine snapshot with wrong ownership or allocation was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
