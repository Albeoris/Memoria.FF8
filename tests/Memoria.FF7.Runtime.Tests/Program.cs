using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

var tests = new (string Name, Action Run)[]
{
    ("envelope preserves both game namespaces and exact FFVIII payload", EnvelopeRoundTrip),
    ("envelope corruption and missing active game rejected", InvalidEnvelope),
    ("world switch requires save permission and prepares before commit", SwitchGuard),
    ("world revisits resume each independent continuation", ResumeBothWorlds),
    ("native movement authored contracts", NativeMovementTests.Run),
    ("native animation authored contracts", NativeAnimationTests.Run),
    ("native dialogue continuation contracts", NativeDialogueTests.Run),
    ("native turn, camera and global process contracts", NativeProcessTests.Run),
    ("native character growth contracts", NativeGrowthTests.Run),
    ("native debug victory and exactly-once rewards", NativeBattleTests.Run),
    ("native field encounter-step contracts", NativeEncounterTests.Run),
    ("native field audio commands and continuation", NativeAudioTests.Run),
    ("installed native movement contracts", NativeMovementTests.RunInstalled),
    ("installed New Game and cold continuation", InstalledSession),
    ("installed opening and cold process continuation", InstalledOpeningTests.Run)
};
int failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine("PASS " + test.Name);
    }
    catch (Exception error)
    {
        failed++;
        Console.Error.WriteLine("FAIL " + test.Name + ": " + error);
    }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} groups passed");
return failed == 0 ? 0 : 1;

static Ff7SessionSave Fixture(long tick = 0)
{
    FieldProgram program = new([0, 0x5F, 0x12, 1], [new ushort[32]]);
    FieldMachine machine = new(program);
    machine.Initialize();
    return new Ff7SessionSave
    {
        ResourceProfile = "fixture",
        Language = "en",
        FieldName = "fixture",
        Progress = new Ff7Progress { InitialKernelData = [1], Characters = [new Ff7CharacterState(0, "Cloud", 6, 100, 314, 54, new byte[132])], Party = [0, 255, 255], Inventory = [new Ff7InventorySlot(0, 2)], Materia = [], Gil = 240 },
        Vm = machine.Capture() with { Tick = tick },
        Process = new Ff7FieldProcess { Tick = tick }
    };
}

static void EnvelopeRoundTrip()
{
    byte[] ff8 = [0x4D, 0x46, 0x46, 0x38, 1, 2, 3, 255];
    CombinedSessionSave save = new() { ActiveGame = SessionGame.Ff7, Ff8Payload = ff8, Ff8PayloadFormat = "fixture-ff8", Ff7 = Fixture(18), Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "standard-field-squall")] };
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, save);
    stream.Position = 0;
    Check(CombinedSessionCodec.HasSignature(stream) && stream.Position == 0, "Signature inspection consumed input.");
    CombinedSessionSave restored = CombinedSessionCodec.Read(stream);
    Check(restored.Ff8Payload!.SequenceEqual(ff8), "FFVIII payload changed.");
    Check(restored.Ff7!.Vm.Tick == 18 && restored.Ff7.Progress.Gil == 240 && restored.Ff7.Progress.Inventory[0].Quantity == 2, "FFVII progression or PC clock changed.");
    Check(restored.Mappings.Single().AppearanceGame == SessionGame.Ff8, "Appearance identity was lost.");
}

static void InvalidEnvelope()
{
    Throws<InvalidDataException>(() => (new CombinedSessionSave { ActiveGame = SessionGame.Ff7 }).Validate());
    CombinedSessionSave source = new() { ActiveGame = SessionGame.Ff7, Ff7 = Fixture() };
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, source);
    byte[] corrupt = stream.ToArray();
    corrupt[^1] ^= 127;
    Throws<InvalidDataException>(() => CombinedSessionCodec.Read(new MemoryStream(corrupt)));
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff7, 1, SessionGame.Ff8, 0, "wrong")] }).Validate());
}

static void SwitchGuard()
{
    WorldSwitch coordinator = new(new CombinedSessionSave { ActiveGame = SessionGame.Ff8, Ff8Payload = [10], Ff8PayloadFormat = "fixture" });
    int created = 0;
    Throws<InvalidOperationException>(() => coordinator.ToFf7(new(false), [11], "fixture", () => { created++; return Fixture(); }, _ => { }));
    Check(created == 0 && coordinator.State.ActiveGame == SessionGame.Ff8, "Denied switch created or advanced a session.");
    Throws<InvalidOperationException>(() => coordinator.ToFf7(new(true), [11], "fixture", FixtureFactory, _ => throw new InvalidOperationException("unavailable destination")));
    Check(coordinator.State.ActiveGame == SessionGame.Ff8 && coordinator.State.Ff8Payload![0] == 10 && coordinator.State.Ff7 is null, "Failed preparation committed a partial world switch.");
}

static Ff7SessionSave FixtureFactory()
{
    return Fixture();
}

static void ResumeBothWorlds()
{
    WorldSwitch coordinator = new(new CombinedSessionSave { ActiveGame = SessionGame.Ff8, Ff8Payload = [1], Ff8PayloadFormat = "fixture" });
    int newGames = 0;
    coordinator.ToFf7(new(true), [2], "fixture", () => { newGames++; return Fixture(0); }, target => Check(target.Vm.Tick == 0, "First visit did not start New Game."));
    coordinator.ToFf8(new(true), Fixture(123), (payload, _) => Check(payload[0] == 2, "FFVIII did not resume its outgoing state."));
    coordinator.ToFf7(new(true), [3], "fixture", () => { newGames++; return Fixture(0); }, target => Check(target.Vm.Tick == 123, "FFVII revisit replayed New Game."));
    Check(newGames == 1 && coordinator.State.Ff8Payload![0] == 3 && coordinator.State.Ff7!.Vm.Tick == 123, "Independent world continuations changed.");
}

static void InstalledSession()
{
    string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
    if (string.IsNullOrWhiteSpace(root))
    {
        Console.WriteLine("SKIP installed New Game: MEMORIA_FF7_GAME_DIR is unset.");
        return;
    }
    Ff7Installation installation = Ff7Installation.Discover(root);
    using Ff7Session original = Ff7Session.NewGame(installation);
    Ff7SessionSave snapshot = original.Capture();
    Console.WriteLine($"initial-field={original.Field.Name} actors={original.Host.Process.Actors.Length} party={string.Join(',', original.Progress.Party)} movie={original.Host.Process.Movie?.PreparedId}");
    Check(original.Field.Name == "md1stin" && snapshot.Vm.PersistentVariables[768] == 1 && original.Progress.Party.SequenceEqual(new[] { 0, 255, 255 }), "Native cold New Game owners were not applied.");
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, new CombinedSessionSave { ActiveGame = SessionGame.Ff7, Ff7 = snapshot });
    stream.Position = 0;
    using Ff7Session restored = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
    original.AdvanceTick();
    restored.AdvanceTick();
    Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Installed New Game cold restore changed the next tick.");
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
